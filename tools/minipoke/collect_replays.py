"""
Collects public random battle replays from replay.pokemonshowdown.com, to train on human games.

Every public replay's JSON carries the battle's input log: the server version (a git commit),
the battle and team seeds, both players' ratings and every choice. Run through Showdown's
simulator at that version, it rebuilds what each player saw, so a replay gives first-person
training states for both sides.

Leave it running (it resumes where it stopped; `caffeinate -is` keeps the Mac awake):

    caffeinate -is python collect_replays.py                     # gen9randombattle, rated 1500+
    python collect_replays.py --min-rating 1600 --oldest-days 90
    python collect_replays.py --summary                          # what is on disk so far

It lists replays newest first (search.json: 51 a page, `before` an upload time, exclusive) and
downloads the ones rated --min-rating or more. Two sweeps share the requests:
- head: every --head-every seconds, the replays uploaded since the last head sweep;
- backfill: older pages, one at a time, back to --oldest-days.
Each replay is saved once, as <out>/<UTC upload day>/<id>.json.gz (the JSON as served).
<out>/state.json holds the two cursors.

Staying polite, so the server has no reason to rate limit or block us:
- one request at a time, on one kept-alive connection, --delay seconds apart (2 s, +-30%
  jitter), gzip, and a User-Agent naming the script (Cloudflare answers Python's default
  one with a 403);
- 429 or 5xx: waits Retry-After, or backs off (1 min, doubling up to 30 min); a 429 also makes
  --delay 1.5x longer for the rest of the run;
- 403 (Cloudflare refusing us): waits 15 min, doubling, and stops after --max-blocks in a row,
  since retrying while blocked only extends a block;
- network errors: reconnects after 5 s, then backs off from 30 s, doubling up to 10 min.
Ctrl-C or SIGTERM stops after the current request; a second Ctrl-C quits at once.

The listed rating is the replay's (close to the two players' average). Each player's own
rating is on its `>player` line of the input log: filter on it when building a dataset.
"""

from __future__ import annotations

import argparse
import gzip
import http.client
import json
import logging
import os
import random
import signal
import sys
import time
from collections import Counter
from dataclasses import asdict, dataclass, fields
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple
from urllib.parse import quote

HOST = "replay.pokemonshowdown.com"
PAGE_SIZE = 51
REPLAY_SUFFIX = ".json.gz"
DEFAULT_OUT = Path(__file__).resolve().parent / "replays" / "human"
DEFAULT_USER_AGENT = (f"minipoke-replay-collector/1.0 (sequential, one request every few seconds; "
                      f"Python {sys.version_info.major}.{sys.version_info.minor})")

log = logging.getLogger("collect_replays")


class Stopped(Exception):
    """A stop was requested (Ctrl-C, SIGTERM, --max-replays)."""


class Blocked(Exception):
    """The server kept refusing us (403): stop rather than make the block longer."""


class StopSignal:
    """A flag set from a signal handler. Not a threading.Event: setting one from a handler can
    deadlock on its lock if the signal lands while the main thread holds it."""

    def __init__(self) -> None:
        self.requested = False

    def set(self) -> None:
        self.requested = True

    def is_set(self) -> bool:
        return self.requested

    def wait(self, seconds: float) -> bool:
        """Sleeps `seconds`, or less if a stop is requested. True when one was."""
        end = time.monotonic() + seconds
        while not self.requested:
            remaining = end - time.monotonic()
            if remaining <= 0:
                return False
            time.sleep(min(remaining, 0.5))
        return True


def retry_after(headers) -> Optional[float]:
    """Retry-After in seconds (a number or an HTTP date), capped at 2 h; None if absent."""
    value = headers.get("Retry-After") if headers is not None else None
    if not value:
        return None
    try:
        seconds = float(value)
    except ValueError:
        try:
            seconds = (parsedate_to_datetime(value) - datetime.now(timezone.utc)).total_seconds()
        except (TypeError, ValueError):
            return None
    return min(max(seconds, 1.0), 7200.0)


class ReplayServer:
    """GETs JSON from the replay server: one request at a time, paced, backing off."""

    def __init__(self, user_agent: str = DEFAULT_USER_AGENT, delay: float = 2.0,
                 stop: Optional[StopSignal] = None, max_blocks: int = 4, timeout: float = 30.0,
                 max_delay: float = 30.0) -> None:
        self.user_agent = user_agent
        self.delay = delay
        self.stop = stop or StopSignal()
        self.max_blocks = max_blocks
        self.timeout = timeout
        self.max_delay = max_delay
        self.requests = 0
        self._conn: Optional[http.client.HTTPSConnection] = None
        self._cookies: Dict[str, str] = {}
        self._next_at = 0.0

    def get(self, path: str):
        """The parsed JSON at `path`, or None when there is nothing to retry (404: the replay
        was deleted or made private; another 4xx is logged)."""
        errors = rate_limits = blocks = 0
        while True:
            self._pace()
            try:
                status, headers, body = self._request(path)
            except (OSError, http.client.HTTPException, ValueError) as error:
                self._close()
                wait = 5.0 if errors == 0 else min(30.0 * 2 ** (errors - 1), 600.0)
                errors += 1
                log.warning("%s: %s (%s), retrying in %.0f s", path, type(error).__name__, error, wait)
                self._sleep(wait)
                continue
            if status == 200:
                try:
                    return json.loads(body)
                except ValueError:
                    # A challenge page or a cut response: treat it like a server error
                    status = 502
            if status == 404:
                return None
            if status == 403:
                blocks += 1
                if blocks >= self.max_blocks:
                    raise Blocked(f"{blocks} refusals (HTTP 403) in a row on {path}")
                wait = min(900.0 * 2 ** (blocks - 1), 7200.0)
            elif status == 429 or status >= 500:
                wait = retry_after(headers) or min(60.0 * 2 ** rate_limits, 1800.0)
                rate_limits += 1
                if status == 429:
                    self.delay = min(self.delay * 1.5, self.max_delay)
            else:
                log.warning("%s: HTTP %d, skipped", path, status)
                return None
            log.warning("%s: HTTP %d, waiting %.0f s (delay between requests now %.1f s)",
                        path, status, wait, self.delay)
            self._sleep(wait)

    def _request(self, path: str) -> Tuple[int, object, bytes]:
        if self._conn is None:
            self._conn = http.client.HTTPSConnection(HOST, timeout=self.timeout)
        headers = {"User-Agent": self.user_agent, "Accept": "application/json", "Accept-Encoding": "gzip"}
        if self._cookies:
            headers["Cookie"] = "; ".join(f"{name}={value}" for name, value in self._cookies.items())
        self.requests += 1
        self._conn.request("GET", path, headers=headers)
        response = self._conn.getresponse()
        body = response.read()
        for cookie in response.headers.get_all("Set-Cookie") or []:
            name, _, value = cookie.split(";", 1)[0].partition("=")
            if name.strip():
                self._cookies[name.strip()] = value.strip()
        if response.headers.get("Content-Encoding", "").lower() == "gzip":
            body = gzip.decompress(body)
        if response.will_close:
            self._close()
        return response.status, response.headers, body

    def _pace(self) -> None:
        wait = self._next_at - time.monotonic()
        if wait > 0:
            self._sleep(wait)
        self._next_at = time.monotonic() + self.delay * random.uniform(0.7, 1.3)

    def _sleep(self, seconds: float) -> None:
        if self.stop.wait(seconds):
            raise Stopped()

    def _close(self) -> None:
        if self._conn is not None:
            self._conn.close()
            self._conn = None


@dataclass
class State:
    newest: Optional[int] = None           # the newest upload time a finished head sweep reached
    backfill_before: Optional[int] = None  # the backfill's next `before`
    backfill_done: bool = False


def next_before(page: List[dict], before: Optional[int]) -> int:
    """The `before` of the page after `page`. `before` is exclusive, so +1 lists again the
    replays uploaded in the same second as the oldest one here (some may not have fit)."""
    oldest = min(entry["uploadtime"] for entry in page)
    cursor = oldest + 1
    return oldest if before is not None and cursor >= before else cursor


def replay_path(out: Path, replay_id: str, upload_time: int) -> Path:
    day = datetime.fromtimestamp(upload_time, timezone.utc).strftime("%Y-%m-%d")
    return out / day / f"{replay_id}{REPLAY_SUFFIX}"


def utc(upload_time: int) -> str:
    return datetime.fromtimestamp(upload_time, timezone.utc).strftime("%Y-%m-%d %H:%M")


class Collector:
    def __init__(self, server: ReplayServer, out: Path, fmt: str = "gen9randombattle", min_rating: int = 1500,
                 oldest: int = 0, head_every: float = 900.0, max_replays: Optional[int] = None) -> None:
        self.server = server
        self.out = out
        self.fmt = fmt
        self.min_rating = min_rating
        self.oldest = oldest
        self.head_every = head_every
        self.max_replays = max_replays
        out.mkdir(parents=True, exist_ok=True)
        for leftover in out.glob("*/*.tmp"):
            leftover.unlink()
        self.known = {path.name[:-len(REPLAY_SUFFIX)] for path in out.glob(f"*/*{REPLAY_SUFFIX}")}
        self.skipped: set = set()  # fetched but not kept (no input log, 404): not fetched again this run
        self.state = self._load_state()
        self.saved = 0
        self.started = time.monotonic()

    def run(self) -> None:
        log.info("collecting %s rated %d+ into %s (%d replays already there)",
                 self.fmt, self.min_rating, self.out, len(self.known))
        next_head = 0.0
        try:
            while not self.server.stop.is_set():
                if time.monotonic() >= next_head:
                    self.head_sweep()
                    next_head = time.monotonic() + self.head_every
                elif not self.state.backfill_done:
                    self.backfill_page()
                else:
                    self.server._sleep(max(0.0, next_head - time.monotonic()))
        except Stopped:
            pass
        finally:
            self._save_state()
            log.info("stopped: %d replays saved this run (%d on disk), %d requests",
                     self.saved, len(self.known), self.server.requests)

    def head_sweep(self) -> None:
        """The replays uploaded since the last finished head sweep, newest first. The first one
        only reads the first page: the backfill starts there."""
        newest = self.state.newest
        before: Optional[int] = None
        top: Optional[int] = None
        counts: Counter = Counter()
        while True:
            page = self.list_page(before)
            if not page:
                break
            times = [entry["uploadtime"] for entry in page]
            top = max(times) if top is None else top
            counts.update(self.process(entry for entry in page if newest is None or entry["uploadtime"] >= newest))
            if newest is None or min(times) < newest or len(page) < PAGE_SIZE:
                break
            before = next_before(page, before)
        if top is not None:
            self.state.newest = max(top, newest or 0)
            if self.state.backfill_before is None:
                self.state.backfill_before = top + 1
        self._save_state()
        self._log_page("head", counts, None)

    def backfill_page(self) -> None:
        page = self.list_page(self.state.backfill_before)
        recent = [entry for entry in page if entry["uploadtime"] >= self.oldest]
        counts = self.process(recent)
        if len(page) < PAGE_SIZE or len(recent) < len(page):
            self.state.backfill_done = True
            log.info("backfill done: reached %s", "the oldest replay" if len(page) < PAGE_SIZE else "--oldest-days")
        else:
            self.state.backfill_before = next_before(page, self.state.backfill_before)
        self._save_state()
        self._log_page("backfill", counts, page)

    def list_page(self, before: Optional[int]) -> List[dict]:
        path = f"/search.json?format={quote(self.fmt)}" + ("" if before is None else f"&before={before}")
        page = self.server.get(path)
        return [entry for entry in page if "id" in entry and "uploadtime" in entry] if isinstance(page, list) else []

    def process(self, entries: Iterable[dict]) -> Counter:
        counts: Counter = Counter()
        for entry in entries:
            counts["listed"] += 1
            if (entry.get("rating") or 0) < self.min_rating or entry.get("private"):
                continue
            counts["rated"] += 1
            replay_id = entry["id"]
            if replay_id in self.known or replay_id in self.skipped:
                continue
            replay = self.server.get(f"/{quote(replay_id)}.json")
            if not self._usable(replay):
                self.skipped.add(replay_id)
                counts["unusable"] += 1
                continue
            self._save(replay, int(replay.get("uploadtime") or entry["uploadtime"]))
            counts["saved"] += 1
            if self.max_replays and self.saved >= self.max_replays:
                self.server.stop.set()
                raise Stopped()
        return counts

    def _usable(self, replay) -> bool:
        return (isinstance(replay, dict) and replay.get("formatid", self.fmt) == self.fmt
                and ">start " in (replay.get("inputlog") or "") and bool(replay.get("log")))

    def _save(self, replay: dict, upload_time: int) -> None:
        path = replay_path(self.out, replay["id"], upload_time)
        path.parent.mkdir(exist_ok=True)
        temporary = path.with_name(path.name + ".tmp")
        temporary.write_bytes(gzip.compress(json.dumps(replay, separators=(",", ":")).encode()))
        os.replace(temporary, path)
        self.known.add(replay["id"])
        self.saved += 1

    def _log_page(self, sweep: str, counts: Counter, page: Optional[List[dict]]) -> None:
        span = ""
        if page:
            times = [entry["uploadtime"] for entry in page]
            span = f"{utc(min(times))} .. {utc(max(times))} UTC  "
        hours = max((time.monotonic() - self.started) / 3600, 1e-9)
        log.info("[%s] %slisted %d, rated %d+: %d, saved %d%s | %d on disk, %.0f/h this run, %d requests, %.1f s apart",
                 sweep, span, counts["listed"], self.min_rating, counts["rated"], counts["saved"],
                 f", unusable {counts['unusable']}" if counts["unusable"] else "",
                 len(self.known), self.saved / hours, self.server.requests, self.server.delay)

    def _state_path(self) -> Path:
        return self.out / "state.json"

    def _load_state(self) -> State:
        try:
            saved = json.loads(self._state_path().read_text())
        except (OSError, ValueError):
            return State()
        names = {field.name for field in fields(State)}
        return State(**{key: value for key, value in saved.items() if key in names})

    def _save_state(self) -> None:
        temporary = self._state_path().with_suffix(".json.tmp")
        temporary.write_text(json.dumps(asdict(self.state), indent=1))
        os.replace(temporary, self._state_path())


def summarize(out: Path) -> str:
    """What is on disk: replays, sides by the player's own rating, days, server versions."""
    replays = 0
    sides: Counter = Counter()
    versions: Counter = Counter()
    days: Counter = Counter()
    for path in out.glob(f"*/*{REPLAY_SUFFIX}"):
        try:
            replay = json.loads(gzip.decompress(path.read_bytes()))
        except (OSError, ValueError):
            continue
        replays += 1
        days[path.parent.name] += 1
        for line in replay.get("inputlog", "").splitlines():
            if line.startswith(">version "):
                versions[line.split(" ", 1)[1].strip()[:12]] += 1
            elif line.startswith(">player "):
                try:
                    rating = int(json.loads(line.split(" ", 2)[2]).get("rating") or 0)
                except (IndexError, ValueError, TypeError):
                    continue
                for threshold in (1500, 1600, 1700, 1800, 2000):
                    sides[threshold] += rating >= threshold
    if not replays:
        return f"no replays in {out}"
    lines = [f"{replays} replays in {out}, uploaded {min(days)} to {max(days)} (UTC)",
             "player sides rated " + ", ".join(f"{threshold}+: {sides[threshold]}" for threshold in sorted(sides)),
             f"{len(versions)} server versions (each needs its own Showdown checkout to re-simulate):"]
    top = versions.most_common(10)
    lines += [f"  {version}  {count}" for version, count in top]
    if len(versions) > len(top):
        lines.append(f"  ... {len(versions) - len(top)} more, {replays - sum(count for _, count in top)} replays")
    return "\n".join(lines)


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--format", default="gen9randombattle")
    parser.add_argument("--min-rating", type=int, default=1500,
                        help="the replay's listed rating to download (default 1500; 0 also takes unrated games)")
    parser.add_argument("--oldest-days", type=float, default=365, help="how far back the backfill goes (default 365)")
    parser.add_argument("--out", type=Path, help="default: replays/human/<format> next to this script")
    parser.add_argument("--delay", type=float, default=2.0, help="seconds between requests (default 2, at least 1)")
    parser.add_argument("--head-every", type=float, default=900, help="seconds between head sweeps (default 900)")
    parser.add_argument("--max-replays", type=int, help="stop after saving this many replays")
    parser.add_argument("--max-blocks", type=int, default=4, help="stop after this many 403s in a row (default 4)")
    parser.add_argument("--user-agent", default=DEFAULT_USER_AGENT)
    parser.add_argument("--summary", action="store_true", help="print what is on disk and exit (no requests)")
    args = parser.parse_args(argv)
    if args.delay < 1:
        parser.error("--delay must be at least 1 second")
    out = args.out or DEFAULT_OUT / args.format

    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s", datefmt="%H:%M:%S")
    if args.summary:
        print(summarize(out))
        return 0

    stop = StopSignal()

    def on_signal(signum, frame):
        if stop.is_set():
            raise KeyboardInterrupt
        log.info("stopping after the current request (Ctrl-C again to quit now)")
        stop.set()

    signal.signal(signal.SIGINT, on_signal)
    signal.signal(signal.SIGTERM, on_signal)

    server = ReplayServer(args.user_agent, args.delay, stop, args.max_blocks)
    collector = Collector(server, out, args.format, args.min_rating, int(time.time() - args.oldest_days * 86400),
                          args.head_every, args.max_replays)
    try:
        collector.run()
    except Blocked as error:
        log.error("%s: stopped. Wait a few hours before running it again, with a longer --delay.", error)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
