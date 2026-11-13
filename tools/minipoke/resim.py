"""
Re-simulates public replays from their input logs, to rebuild what each player saw.

    python resim.py check replays/human/gen9randombattle replays/human/gen9randombattle-first-step-zipped
    python resim.py parity replays/replays               # the bot's games against its own .logs

A replay's input log (collect_replays.py) holds the server version, the battle and team seeds,
both players and every committed choice. `resim.js` writes it, line by line, into that version's
own BattleStream (a checkout per version: showdown_versions.py) and returns what each player's
websocket received: the updates with |split| lines resolved for that player, and its requests
with the rqid Showdown's server adds. Lines only the server adds (room joins, chat, the timer,
forfeit messages) are not simulated, so comparisons leave them out (`battle_lines`).

A re-simulation is kept when:
 - its version could be built;
 - every input log choice was accepted and answered a request (`choice_errors` empty);
 - the battle ended (|win| or |tie|);
 - its omniscient view (exact HP of both sides) equals the replay's public log, line for line,
   once both are reduced to battle lines (`battle_lines`; timestamps differ). A public replay's
   log is the omniscient view, with the server's own lines.
`check` reports the drop rate per version and why.

`parity` re-simulates the bot's own ladder games (`-savereplays`: <id>.json with the input log,
<id>.log with every line ElsaMina received live) and checks that the bot's side of the
re-simulation is the .log's battle lines exactly, then that poke-env, fed either one, makes the
same decisions with the same encodings, and that the re-simulation's choices map to the same
actions as analyze_replays.py's alignment of the .log. A model only plays well on the exact
state it was trained on: that is what makes human replays usable as training states.

Rejected choices never reach the input log (Showdown logs a choice when it commits it), so a
battle where the bot's choice was refused (its .log has an |error| and a second request) differs
by those two lines: `parity` reports such battles apart.
"""

from __future__ import annotations

import argparse
import gzip
import json
import logging
import re
import subprocess
import time
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass, field
from pathlib import Path
from queue import Queue
from typing import Dict, Iterable, Iterator, List, Optional, Sequence, Tuple

from showdown_versions import BuildFailed, ShowdownVersions, short

log = logging.getLogger("minipoke")
RESIM_SCRIPT = Path(__file__).resolve().parent / "resim.js"
SIDES = ("p1", "p2")

# Line types the room adds, never the simulator
SERVER_ONLY = {"j", "J", "l", "L", "n", "N", "c", "c:", "chat", "t:", "inactive", "inactiveoff",
               "raw", "html", "uhtml", "uhtmlchange", "init", "title", "join", "leave", "name",
               "deinit", "noinit", "unlink", "hidelines", "notify", "tempnotify", "tempnotifyoff",
               "expire", "updatechallenges", "badge"}
# |-message| lines of room-battle.ts (forfeitPlayer, the timer, renames)
SERVER_MESSAGE = re.compile(r"^.+ (forfeited\.|lost due to inactivity\.|was made to forfeit\.|"
                            r"forfeited by changing their name\.|lost by having an inappropriate name\.)$")
SERVER_HINT = "If you want to tie earlier, consider using `/offertie`."


def line_kind(line: str) -> str:
    parts = line.split("|", 2)
    return parts[1] if len(parts) > 1 and line.startswith("|") else ""


def battle_lines(lines: Sequence[str], requests: bool = True) -> List[str]:
    """The lines the simulator produced: the room's own lines removed (joins, chat, timer,
    timestamps, forfeit messages, the |player| line of a player rejoining), and with
    `requests` False the |request| and |error| lines too."""
    out = []
    started = False
    for line in lines:
        # a log's trailing newline (the simulator's blank lines are "|"), the plain text a chat
        # command answers with, and the room's messages ("||Ladder isn't responding...")
        if not line.startswith("|") or line.startswith("||"):
            continue
        kind = line_kind(line)
        if kind in SERVER_ONLY:
            continue
        if kind == "-message" and SERVER_MESSAGE.match(line[len("|-message|"):]):
            continue
        if kind == "-hint" and line[len("|-hint|"):] == SERVER_HINT:
            continue
        if kind == "player" and (started or line.count("|") < 3 or line.split("|")[3] == ""):
            continue              # a player leaving (no name) or rejoining: the room's line
        if not requests and kind in ("request", "error"):
            continue
        started = started or kind == "start"
        out.append(line)
    return out


def first_difference(a: Sequence[str], b: Sequence[str]) -> Optional[str]:
    """None when equal, else where they first differ."""
    for i, (x, y) in enumerate(zip(a, b)):
        if x != y:
            return f"line {i}: {x[:120]!r} != {y[:120]!r}"
    if len(a) != len(b):
        return f"lengths {len(a)} != {len(b)}"
    return None


# --------------------------------------------------------------------------
# Replays
# --------------------------------------------------------------------------
@dataclass
class Replay:
    id: str
    path: Path
    upload_time: int
    version: str                      # the server's commit (">version"), "" if none
    inputlog: str
    log: List[str]                    # the public log (omniscient view + the room's lines)
    players: Dict[str, dict]          # side -> its ">player" JSON (name, rating, seed)

    def rating(self, side: str) -> Optional[int]:
        value = self.players.get(side, {}).get("rating")
        try:
            return int(value) if value not in (None, "") else None
        except (TypeError, ValueError):
            return None

    def name(self, side: str) -> str:
        return self.players.get(side, {}).get("name", side)


def read_replay(path: Path) -> Replay:
    raw = path.read_bytes()
    data = json.loads(gzip.decompress(raw) if path.suffix == ".gz" else raw)
    inputlog = data.get("inputlog") or ""
    version, players = "", {}
    for line in inputlog.split("\n"):
        if line.startswith(">version "):
            version = line.split(" ", 1)[1].strip()
        elif line.startswith(">player "):
            _, side, payload = line.split(" ", 2)
            players[side] = json.loads(payload)
    log_lines = (data.get("log") or "").split("\n")
    return Replay(data.get("id") or path.name.split(".")[0], path, int(data.get("uploadtime") or 0),
                  version, inputlog, log_lines, players)


def replay_files(paths: Iterable[Path]) -> Tuple[List[Path], int]:
    """Every replay file under `paths` (folders at any depth, or files), one per replay id: the
    first one found. Returns them and the number of duplicates skipped."""
    seen, out, dupes = set(), [], 0
    for root in paths:
        root = Path(root)
        files = [root] if root.is_file() else sorted(
            p for p in root.rglob("*") if p.name.endswith((".json.gz", ".json")) and p.is_file()
            and not p.name.startswith(("._", "state.")))
        for path in files:
            stem = path.name.split(".json")[0]
            if stem in seen:
                dupes += 1
                continue
            seen.add(stem)
            out.append(path)
    return out, dupes


# --------------------------------------------------------------------------
# Node workers
# --------------------------------------------------------------------------
class ResimWorker:
    """One `node resim.js <checkout>` process: one battle at a time."""

    def __init__(self, checkout: Path, node: str = "node"):
        self.proc = subprocess.Popen([node, str(RESIM_SCRIPT), str(checkout)], stdin=subprocess.PIPE,
                                     stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
                                     bufsize=1)

    def resim(self, battle_id: str, inputlog: str) -> dict:
        self.proc.stdin.write(json.dumps({"id": battle_id, "inputlog": inputlog}) + "\n")
        self.proc.stdin.flush()
        line = self.proc.stdout.readline()
        if not line:
            err = self.proc.stderr.read()[-500:] if self.proc.poll() is not None else ""
            raise RuntimeError(f"resim.js died: {err}")
        return json.loads(line)

    def close(self) -> None:
        try:
            self.proc.stdin.close()
            self.proc.wait(timeout=10)
        except (OSError, subprocess.TimeoutExpired):
            self.proc.kill()


@dataclass
class Resim:
    """A replay's re-simulation and its verdict."""
    replay: Replay
    status: str                       # ok, build_failed, error, choice_errors, not_ended, mismatch, no_version
    detail: str = ""
    sides: Dict[str, List[str]] = field(default_factory=dict)
    requests: Dict[str, List[dict]] = field(default_factory=dict)
    omniscient: List[str] = field(default_factory=list)

    @property
    def ok(self) -> bool:
        return self.status == "ok"


def judge(replay: Replay, out: dict, check_log: bool = True) -> Resim:
    res = Resim(replay, "ok", sides=out.get("sides") or {}, requests=out.get("requests") or {},
                omniscient=out.get("omniscient") or [])
    if not out.get("ok"):
        res.status, res.detail = "error", out.get("error") or ""
    elif out.get("choice_errors"):
        res.status, res.detail = "choice_errors", out["choice_errors"][0]
    elif not out.get("ended"):
        res.status = "not_ended"
    elif check_log:
        # A public log's |error| lines answer chat commands typed in the room: the simulator's
        # only go to the player concerned, never into the log
        diff = first_difference(battle_lines(replay.log, requests=False),
                                battle_lines(res.omniscient, requests=False))
        if diff is not None:
            res.status, res.detail = "mismatch", diff
    return res


def resimulate(replays: Sequence[Replay], versions: ShowdownVersions, workers: int = 4,
               check_log: bool = True, retry_builds: bool = False) -> Iterator[Resim]:
    """Re-simulates `replays`, version by version (each built once), `workers` Node processes at
    a time. Yields every replay's Resim (in each version's order)."""
    by_version: Dict[str, List[Replay]] = defaultdict(list)
    for replay in replays:
        by_version[replay.version].append(replay)
    for version, group in sorted(by_version.items(), key=lambda kv: -len(kv[1])):
        if not version:
            for replay in group:
                yield Resim(replay, "no_version", "the input log has no >version")
            continue
        try:
            checkout = versions.ensure(version, retry=retry_builds)
        except BuildFailed as exc:
            for replay in group:
                yield Resim(replay, "build_failed", str(exc)[:300])
            continue
        log.info("[resim] %s: %d battles", short(version), len(group))
        yield from _run_version(checkout, group, workers, check_log)


def _run_version(checkout: Path, group: Sequence[Replay], workers: int, check_log: bool) -> Iterator[Resim]:
    pool: Queue = Queue()
    made = [ResimWorker(checkout) for _ in range(max(1, min(workers, len(group))))]
    for w in made:
        pool.put(w)

    def one(replay: Replay) -> Resim:
        worker = pool.get()
        try:
            return judge(replay, worker.resim(replay.id, replay.inputlog), check_log)
        except (RuntimeError, ValueError, OSError) as exc:
            worker.close()
            worker = ResimWorker(checkout)
            made.append(worker)
            return Resim(replay, "error", str(exc)[:300])
        finally:
            pool.put(worker)

    try:
        with ThreadPoolExecutor(len(made)) as executor:
            yield from executor.map(one, group)
    finally:
        for w in made:
            w.close()


# --------------------------------------------------------------------------
# What a player decided: poke-env replays a side's lines (analyze_replays.ReplayPlayer)
# --------------------------------------------------------------------------
def side_decisions(lines: Sequence[str], username: str, encoders: Dict[str, object],
                   battle_id: str = "gen9randombattle-1") -> dict:
    """poke-env 0.16.1 fed `lines` one at a time, as a client receives them: its decision points
    (analyze_replays.snapshot, each with "obs": {key: encoding}), parse errors and final faint
    counts. A decision's "line" is the index in `lines` of the |request| that asked for it.
    poke-env reads the format from the room name, battle-<battle_id>: keep the format in it.

    The room sends |init|battle first, where poke-env counts the battle it then waits for at
    |win|: a re-simulated side (no room lines) gets it prepended, as a client joining would."""
    import asyncio

    from poke_env.concurrency import POKE_LOOP

    import analyze_replays as A
    import encoder as E
    prefix = [] if lines and lines[0].startswith("|init|") else ["|init|battle"]
    tag = f"battle-{battle_id}"
    player = A.ReplayPlayer(username, encoders)
    errors = asyncio.run_coroutine_threadsafe(player.replay(tag, prefix + list(lines)), POKE_LOOP).result()
    for d in player.decisions:
        d["line"] -= len(prefix)
    errors = [(i - len(prefix), text) for i, text in errors]
    battle = player.battles.get(tag)
    return {"decisions": player.decisions, "parse_errors": errors, "messages": player.messages,
            "final_fainted": list(E.fainted_counts(battle)) if battle is not None else [0, 0],
            "battle": battle}


def answered_choices(decisions: Sequence[dict], requests: Sequence[dict]) -> Tuple[List[Optional[str]], int]:
    """The input log choice answering each decision point, from resim.js's request records
    (matched on the |request| line). Returns them and the number of decisions on a line that is
    no non-wait request (poke-env decided where the simulator asked nothing: should be 0)."""
    by_line = {r["line"]: r for r in requests}
    out, strays = [], 0
    for d in decisions:
        record = by_line.get(d["line"])
        if record is None or record["wait"]:
            strays += 1
            out.append(None)
        else:
            out.append(record["choice"])
    return out, strays


def as_choice(side: str, text: Optional[str]):
    """An input log choice ("move knockoff terastallize") as analyze_replays.Choice."""
    import analyze_replays as A
    return None if text is None else A.parse_choice(f">{side} {text}")


# --------------------------------------------------------------------------
# check: the drop rate per version
# --------------------------------------------------------------------------
def cmd_check(args) -> None:
    paths, dupes = replay_files(args.replays)
    replays = []
    for path in paths[: args.limit] if args.limit else paths:
        try:
            replays.append(read_replay(path))
        except (OSError, ValueError, KeyError) as exc:
            log.warning("%s: unreadable (%s)", path, exc)
    print(f"{len(replays)} replays ({dupes} duplicate ids skipped)")
    versions = ShowdownVersions(args.versions)
    t0 = time.time()
    stats: Dict[str, Counter] = defaultdict(Counter)
    examples: Dict[Tuple[str, str], str] = {}
    for res in resimulate(replays, versions, args.workers, retry_builds=args.retry_builds):
        version = short(res.replay.version) or "(none)"
        stats[version][res.status] += 1
        if not res.ok:
            examples.setdefault((version, res.status), f"{res.replay.id}: {res.detail}")
    print(f"re-simulated in {time.time() - t0:.0f} s\n")
    statuses = sorted({s for c in stats.values() for s in c} - {"ok"})
    print(f"{'version':14s} {'battles':>7s} {'kept':>6s} {'dropped':>8s}  " + "  ".join(statuses))
    total = Counter()
    for version, counts in sorted(stats.items(), key=lambda kv: -sum(kv[1].values())):
        n = sum(counts.values())
        total.update(counts)
        print(f"{version:14s} {n:7d} {counts['ok']:6d} {1 - counts['ok'] / n:7.1%}  "
              + "  ".join(f"{counts[s]:{len(s)}d}" for s in statuses))
    n = sum(total.values())
    print(f"{'total':14s} {n:7d} {total['ok']:6d} {1 - total['ok'] / max(n, 1):7.1%}  "
          + "  ".join(f"{total[s]:{len(s)}d}" for s in statuses))
    if examples:
        print("\nan example of each drop:")
        for (version, status), text in sorted(examples.items()):
            print(f"  {version} {status}: {text[:220]}")


# --------------------------------------------------------------------------
# parity: the bot's games
# --------------------------------------------------------------------------
@dataclass
class ParityResult:
    stem: str
    status: str            # same, error_lines (a rejected choice), lines, decisions, resim
    detail: str = ""
    decisions: int = 0
    arrays: int = 0        # encoder arrays compared
    actions: int = 0       # committed choices mapped by both


def parity_battle(stem: str, log_lines: Sequence[str], res: Resim,
                  encoders: Dict[str, object]) -> ParityResult:
    """The bot's side of the re-simulation `res` against its .log: battle lines, then poke-env's
    decision points, every encoding, and the action each committed choice maps to."""
    import numpy as np

    import analyze_replays as A
    replay = res.replay
    if not res.ok:
        return ParityResult(stem, "resim", f"{res.status}: {res.detail}")
    info = A.parse_log(log_lines)
    side = info.our_side
    live = battle_lines(log_lines)
    sim = battle_lines(res.sides[side])
    diff = first_difference(live, sim)
    if diff is not None:
        status = "error_lines" if info.errors else "lines"
        return ParityResult(stem, status, diff)
    ours = side_decisions(log_lines, info.our_name, encoders, stem)
    theirs = side_decisions(res.sides[side], replay.name(side), encoders, stem)
    a, b = ours["decisions"], theirs["decisions"]
    if len(a) != len(b):
        return ParityResult(stem, "decisions", f"{len(a)} decision points live, {len(b)} re-simulated")
    arrays = 0
    for k, (x, y) in enumerate(zip(a, b)):
        for key in ("turn", "team", "request_team", "moves", "force_switch", "trapped", "fainted"):
            if x[key] != y[key]:
                return ParityResult(stem, "decisions", f"decision {k}: {key} {x[key]} != {y[key]}")
        for enc in encoders:
            for name, arr in x["obs"][enc].items():
                if not np.array_equal(arr, y["obs"][enc][name]):
                    return ParityResult(stem, "decisions", f"decision {k}: {enc} {name} differs")
                arrays += 1
    # Actions: analyze_replays' alignment of the .log with the input log, against resim.js's
    choices = replay_choices(replay, side)
    statuses, index, _ = A.align_choices([d["line"] for d in a], [i for i, _ in info.errors], len(choices))
    sim_choices, strays = answered_choices(b, res.requests[side])
    if strays:
        return ParityResult(stem, "decisions", f"{strays} decision points on no request")
    mapped = 0
    for k, (x, y) in enumerate(zip(a, b)):
        live_choice = choices[index[k]] if index[k] is not None else None
        sim_choice = as_choice(side, sim_choices[k])
        if (live_choice is None) != (sim_choice is None) or (
                live_choice is not None and live_choice.raw != sim_choice.raw):
            return ParityResult(stem, "decisions", f"decision {k}: choice "
                                f"{getattr(live_choice, 'raw', None)} != {getattr(sim_choice, 'raw', None)}")
        if live_choice is not None:
            live_action = A.choice_to_action(live_choice, x["team"], x["request_team"], x["moves"])
            sim_action = A.choice_to_action(sim_choice, y["team"], y["request_team"], y["moves"])
            if live_action != sim_action:
                return ParityResult(stem, "decisions", f"decision {k}: action {live_action} != {sim_action}")
            mapped += 1
    return ParityResult(stem, "same", decisions=len(a), arrays=arrays, actions=mapped)


def replay_choices(replay: Replay, side: str):
    import analyze_replays as A
    out = []
    for line in replay.inputlog.split("\n"):
        choice = A.parse_choice(line)
        if choice is not None and choice.side == side:
            out.append(choice)
    return out


def parity_encoders(models: Sequence[str]) -> Dict[str, object]:
    """The encoders of released models (folders under src/ElsaMina.Battles/Models), by name."""
    import analyze_replays as A
    from make_fixtures import encoder_from_spec
    return {name: encoder_from_spec(str(A.MODELS_DIR / name / "encoder_spec.json")) for name in models}


def cmd_parity(args) -> None:
    import analyze_replays as A
    folder = Path(args.folder)
    stems, issues = A.pair_files(folder)
    if args.limit:
        stems = stems[: args.limit]
    encoders = parity_encoders(args.models)
    versions = ShowdownVersions(args.versions)
    loaded = []
    for stem in stems:
        try:
            lines, _, _ = A.load_pair(folder, stem)
        except A.BrokenPair as exc:
            print(f"{stem}: skipped ({exc})")
            continue
        loaded.append((stem, lines, read_replay(folder / f"{stem}.json")))
    t0 = time.time()
    by_id = {r.replay.id: r for r in resimulate([replay for _, _, replay in loaded], versions,
                                                 args.workers)}
    results = [parity_battle(stem, lines, by_id[replay.id], encoders) for stem, lines, replay in loaded]
    counts = Counter(r.status for r in results)
    same = [r for r in results if r.status == "same"]
    print(f"{len(results)} battles in {time.time() - t0:.0f} s: " + ", ".join(f"{k} {v}" for k, v in counts.most_common()))
    print(f"identical: {len(same)} battles, {sum(r.decisions for r in same)} decision points, "
          f"{sum(r.arrays for r in same)} encoder arrays ({', '.join(encoders)}), "
          f"{sum(r.actions for r in same)} committed choices mapped to the same action")
    for r in results:
        if r.status != "same":
            print(f"  {r.stem} {r.status}: {r.detail[:200]}")


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    def common(p):
        p.add_argument("--versions", type=Path, default=None,
                       help="the Showdown checkouts folder (default showdown-versions/ here)")
        p.add_argument("--workers", type=int, default=4, help="Node processes at a time")
        p.add_argument("--limit", type=int, default=None, help="only the first N battles")

    p = sub.add_parser("check", help="re-simulate replays, compare with their logs, drop rate per version")
    common(p)
    p.add_argument("replays", nargs="+", type=Path, help="folders (any depth) or files of replays")
    p.add_argument("--retry-builds", action="store_true", help="try again the versions that failed to build")
    p.set_defaults(fn=cmd_check)

    p = sub.add_parser("parity", help="the bot's ladder games (-savereplays) against their .logs")
    common(p)
    p.add_argument("folder", help="the folder of <id>.log / <id>.json pairs")
    p.add_argument("--models", nargs="+", default=["MiniPokeV1_1M", "MiniPokeV2_1_1M"],
                   help="encoders compared: these released models' (src/ElsaMina.Battles/Models)")
    p.set_defaults(fn=cmd_parity)
    return ap


def main(argv: Optional[List[str]] = None) -> None:
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s", datefmt="%H:%M:%S")
    args = build_parser().parse_args(argv)
    if args.versions is None:
        from showdown_versions import DEFAULT_ROOT
        args.versions = DEFAULT_ROOT
    args.fn(args)


if __name__ == "__main__":
    main()
