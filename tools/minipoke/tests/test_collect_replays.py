"""collect_replays.py against a fake replay server that pages like the real one (newest first,
51 a page, `before` exclusive), and ReplayServer's backoff on scripted HTTP responses."""

import gzip
import json
from urllib.parse import parse_qs, urlparse

import pytest

from collect_replays import (PAGE_SIZE, Blocked, Collector, ReplayServer, StopSignal, Stopped, next_before,
                             summarize)

INPUT_LOG = ('>version 567c1cedddeaa2f35576005d3bfd526cf4f8a13e\n>start {"formatid":"gen9randombattle"}\n'
             '>player p1 {"name":"a","rating":1650}\n>player p2 {"name":"b","rating":1480}\n>p1 move 1\n>p2 move 2')


def make_replays(count, newest=1_000_000, first=0):
    """Two replays per upload second, so pages end in the middle of a second. Every other one is
    rated 1600, and every seventh rated one has no input log."""
    replays = []
    for i in range(first, first + count):
        rated = i % 2 == 0
        replays.append({"id": f"gen9randombattle-{i}", "uploadtime": newest - (i - first) // 2,
                        "rating": 1600 if rated else 1400,
                        "inputlog": None if rated and i % 7 == 0 else INPUT_LOG})
    return replays


def expected_ids(replays, oldest=0):
    return {r["id"] for r in replays if r["rating"] >= 1500 and r["inputlog"] and r["uploadtime"] >= oldest}


class FakeServer:
    def __init__(self, replays):
        self.replays = list(replays)
        self.stop = StopSignal()
        self.requests = 0
        self.delay = 2.0
        self.listed = []
        self.fetched = []

    def get(self, path):
        self.requests += 1
        if path.startswith("/search.json"):
            query = parse_qs(urlparse(path).query)
            before = int(query["before"][0]) if "before" in query else None
            self.listed.append(before)
            rows = sorted((r for r in self.replays if before is None or r["uploadtime"] < before),
                          key=lambda r: -r["uploadtime"])[:PAGE_SIZE]
            return [{"id": r["id"], "uploadtime": r["uploadtime"], "rating": r["rating"], "private": 0} for r in rows]
        replay_id = path[1:-len(".json")]
        self.fetched.append(replay_id)
        replay = next((r for r in self.replays if r["id"] == replay_id), None)
        if replay is None:
            return None
        return {"id": replay_id, "formatid": "gen9randombattle", "uploadtime": replay["uploadtime"],
                "rating": replay["rating"], "log": "|start\n|turn|1\n|turn|2", "inputlog": replay["inputlog"]}

    def _sleep(self, seconds):
        if self.stop.is_set():
            raise Stopped()


def collect_all(collector, limit=50):
    collector.head_sweep()
    for _ in range(limit):
        if collector.state.backfill_done:
            return
        collector.backfill_page()
    raise AssertionError("the backfill never finished")


def saved_ids(out):
    return {path.name[:-len(".json.gz")] for path in out.glob("*/*.json.gz")}


def test_first_run_saves_each_rated_replay_with_an_input_log_once(tmp_path):
    replays = make_replays(130)
    server = FakeServer(replays)
    collect_all(Collector(server, tmp_path))

    assert saved_ids(tmp_path) == expected_ids(replays)
    # Pages listed again from the oldest second (ties) never download a replay twice
    assert len(server.fetched) == len(set(server.fetched))
    assert set(server.fetched) == {r["id"] for r in replays if r["rating"] >= 1500}
    path = next(tmp_path.glob("*/*.json.gz"))
    assert ">start " in json.loads(gzip.decompress(path.read_bytes()))["inputlog"]
    assert path.parent.name == "1970-01-12"  # the UTC day of upload time ~1,000,000


def test_restart_downloads_nothing_already_on_disk(tmp_path):
    replays = make_replays(130)
    collect_all(Collector(FakeServer(replays), tmp_path))

    server = FakeServer(replays)
    collector = Collector(server, tmp_path)
    collector.head_sweep()

    assert collector.state.backfill_done
    # Only the head sweep's newest second is looked at again; its replay without an input log is
    # fetched again (unusable replays are only remembered within a run)
    assert server.fetched == ["gen9randombattle-0"]


def test_head_sweep_collects_new_replays_down_to_the_newest_one_seen(tmp_path):
    old = make_replays(60, newest=1_000_000)
    server = FakeServer(old)
    collector = Collector(server, tmp_path)
    collect_all(collector)
    new = make_replays(80, newest=1_000_100, first=1000)
    server.replays += new
    server.listed.clear()

    collector.head_sweep()

    assert saved_ids(tmp_path) == expected_ids(old) | expected_ids(new)
    assert len(server.listed) == 2  # 51 new, then 29 new and the replays already seen
    assert collector.state.newest == 1_000_100


def test_backfill_stops_at_the_oldest_upload_time(tmp_path):
    replays = make_replays(200)
    oldest = 1_000_000 - 70
    collector = Collector(FakeServer(replays), tmp_path, oldest=oldest)
    collect_all(collector)

    assert saved_ids(tmp_path) == expected_ids(replays, oldest)


def test_max_replays_stops_the_collector(tmp_path):
    server = FakeServer(make_replays(130))
    collector = Collector(server, tmp_path, max_replays=3)
    with pytest.raises(Stopped):
        collect_all(collector)
    assert len(saved_ids(tmp_path)) == 3
    assert server.stop.is_set()


def test_next_before_lists_the_oldest_second_again_unless_stuck():
    page = [{"uploadtime": 105}, {"uploadtime": 100}]
    assert next_before(page, None) == 101
    assert next_before(page, 120) == 101
    assert next_before(page, 101) == 100


def test_summary_counts_sides_by_their_own_rating(tmp_path):
    collect_all(Collector(FakeServer(make_replays(20)), tmp_path))
    summary = summarize(tmp_path)
    count = len(expected_ids(make_replays(20)))
    assert f"{count} replays" in summary
    assert f"1500+: {count}," in summary and f"1600+: {count}," in summary and "1700+: 0" in summary
    assert "567c1cedddea" in summary


class ScriptedServer(ReplayServer):
    def __init__(self, responses, **options):
        super().__init__(**options)
        self.responses = list(responses)
        self.sleeps = []

    def _request(self, path):
        self.requests += 1
        response = self.responses.pop(0)
        if isinstance(response, Exception):
            raise response
        return response

    def _sleep(self, seconds):
        self.sleeps.append(seconds)


def test_rate_limit_waits_retry_after_and_slows_down():
    server = ScriptedServer([(429, {"Retry-After": "7"}, b""), (200, {}, b"[1]")], delay=2.0)
    assert server.get("/x.json") == [1]
    assert 7.0 in server.sleeps
    assert server.delay == 3.0


def test_server_errors_back_off_without_retry_after():
    server = ScriptedServer([(503, {}, b""), (502, {}, b""), (200, {}, b"{}")])
    assert server.get("/x.json") == {}
    assert [s for s in server.sleeps if s >= 60] == [60.0, 120.0]
    assert server.delay == 2.0


def test_repeated_refusals_stop_the_collector():
    server = ScriptedServer([(403, {}, b"")] * 3, max_blocks=3)
    with pytest.raises(Blocked):
        server.get("/x.json")
    assert [s for s in server.sleeps if s >= 900] == [900.0, 1800.0]


def test_network_errors_retry_and_a_missing_replay_is_none():
    server = ScriptedServer([ConnectionResetError(), (200, {}, b"not json"), (404, {}, b"")])
    assert server.get("/x.json") is None
    assert 5.0 in server.sleeps and 60.0 in server.sleeps
