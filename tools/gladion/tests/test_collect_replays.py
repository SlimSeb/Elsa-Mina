"""collect_replays.py against a fake replay server that pages like the real one (newest first,
51 a page, `before` exclusive), and ReplayServer's backoff on scripted HTTP responses."""

import argparse
import gzip
import json
import shutil
import zipfile
from urllib.parse import parse_qs, urlparse

import pytest

from collect_replays import (PAGE_SIZE, Blocked, Collector, ReplayServer, StopSignal, Stopped, archived_ids,
                             next_before, parse_date, summarize)

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
    collect_all(collector)

    # The head sweep looks at its newest second again, and the finished backfill lists its last page
    # once more (the floor may have been lowered) before being done again. Nothing on disk is
    # downloaded: only replays without an input log, remembered within a run only, are fetched.
    unusable = {r["id"] for r in replays if not r["inputlog"]}
    assert server.fetched and set(server.fetched) <= unusable
    assert len(server.listed) == 2


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


def test_replays_archived_in_a_folder_or_a_zip_are_not_downloaded_again(tmp_path):
    replays = make_replays(130)
    collect_all(Collector(FakeServer(replays), tmp_path / "first"))
    days = sorted((tmp_path / "first").glob("19*"))
    # Half the days moved to a folder (one level deeper), the other half zipped with a top folder
    (tmp_path / "archive" / "batch").mkdir(parents=True)
    shutil.move(str(days[0]), str(tmp_path / "archive" / "batch" / days[0].name))
    with zipfile.ZipFile(tmp_path / "batch2.zip", "w") as archive:
        for day in days[1:]:
            for path in day.iterdir():
                archive.write(path, f"batch2/{day.name}/{path.name}")
    archived = archived_ids(tmp_path / "archive") | archived_ids(tmp_path / "batch2.zip")
    assert archived == expected_ids(replays)

    server = FakeServer(replays)
    collector = Collector(server, tmp_path / "second", skip_ids=archived)
    collect_all(collector)

    assert saved_ids(tmp_path / "second") == set()
    # Only the replays without an input log, which no archive holds, are fetched again
    assert set(server.fetched) == {r["id"] for r in replays if r["rating"] >= 1500 and not r["inputlog"]}


def test_archived_ids_ignores_macos_resource_forks_and_refuses_other_files(tmp_path):
    with zipfile.ZipFile(tmp_path / "a.zip", "w") as archive:
        archive.writestr("batch/2026-11-01/gen9randombattle-1.json.gz", b"")
        archive.writestr("__MACOSX/batch/2026-11-01/._gen9randombattle-1.json.gz", b"")
        archive.writestr("batch/state.json", b"{}")
    assert archived_ids(tmp_path / "a.zip") == {"gen9randombattle-1"}
    (tmp_path / "notes.txt").write_text("not an archive")
    with pytest.raises(ValueError):
        archived_ids(tmp_path / "notes.txt")


def test_dates_skip_the_span_collected_elsewhere(tmp_path):
    replays = make_replays(300)  # upload times 1,000,000 down to 999,851
    elsewhere_oldest, elsewhere_newest = 999_960, 999_980
    server = FakeServer(replays)
    collect_all(Collector(server, tmp_path, backfill_from=elsewhere_oldest, head_from=elsewhere_newest))

    upload_time = {r["id"]: r["uploadtime"] for r in replays}
    outside = {rid for rid, t in upload_time.items() if t < elsewhere_oldest or t >= elsewhere_newest}
    assert saved_ids(tmp_path) == expected_ids(replays) & outside
    assert all(upload_time[rid] < elsewhere_oldest or upload_time[rid] >= elsewhere_newest for rid in server.fetched)


def test_restarting_with_the_same_dates_keeps_the_progress(tmp_path):
    replays = make_replays(300)
    first = Collector(FakeServer(replays), tmp_path, backfill_from=999_960, head_from=999_990)
    first.head_sweep()
    first.backfill_page()
    progress = first.state.backfill_before
    assert progress < 999_960

    second = Collector(FakeServer(replays), tmp_path, backfill_from=999_960, head_from=999_990)
    assert second.state.backfill_before == progress
    assert second.state.newest == 1_000_000


def test_a_lower_floor_resumes_a_finished_backfill(tmp_path):
    replays = make_replays(300)
    collect_all(Collector(FakeServer(replays), tmp_path, oldest=999_950))
    assert saved_ids(tmp_path) == expected_ids(replays, 999_950)

    collector = Collector(FakeServer(replays), tmp_path, oldest=999_900)
    assert not collector.state.backfill_done
    collect_all(collector)
    assert saved_ids(tmp_path) == expected_ids(replays, 999_900)


@pytest.mark.parametrize("text, expected", [
    ("2026-10-30", 1793318400),
    ("2026-10-30 13:08", 1793365680),
    ("2026-10-30 13:08:14", 1793365694),
    ("2026-10-30T13:08:14Z", 1793365694),
    ("2026-10-30 13:08:14 UTC", 1793365694),
    ("1793365694", 1793365694),
])
def test_parse_date_reads_utc_dates_and_unix_times(text, expected):
    assert parse_date(text) == expected


def test_parse_date_refuses_other_formats():
    with pytest.raises(argparse.ArgumentTypeError):
        parse_date("30/10/2026")


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
