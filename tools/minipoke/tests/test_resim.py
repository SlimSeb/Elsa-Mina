"""resim.py: re-simulating replays, comparing them with what the bot received live (parity),
and the filters that leave the room's own lines out.

tests/fixtures/resim/ holds three of ElsaMina's ladder games (Showdown 3da4dd39a2), player names
anonymized (Player1 / Player2) and chat removed, in the input log and both logs alike:
    <id>.log              every line the bot received (ReplaySavingService), first person
    <id>.json             the public replay: input log, public log
    <id>.resim.json.gz    resim.js's output for that input log
gen9randombattle-2704377128: the bot is p1 and its opponent forfeits; -2704186278: p1, every
Pokémon fainted; -2704372311: p2. Each terastallizes.

The parity check against the stored re-simulation always runs. Re-simulating needs Node and that
Showdown version, built once by `python resim.py parity tests/fixtures/resim` (showdown_versions.py).
"""

import gzip
import json
import shutil
from pathlib import Path

import numpy as np
import pytest

import analyze_replays as A
import resim as R
from showdown_versions import BuildFailed, ShowdownVersions

FIXTURES = Path(__file__).resolve().parent / "fixtures" / "resim"
STEMS = sorted(p.name[: -len(".log")] for p in FIXTURES.glob("*.log"))
VERSION = "3da4dd39a2e6"


def stored(stem: str) -> dict:
    return json.loads(gzip.decompress((FIXTURES / f"{stem}.resim.json.gz").read_bytes()))


def resim_of(stem: str, out: dict) -> R.Resim:
    return R.judge(R.read_replay(FIXTURES / f"{stem}.json"), out)


@pytest.fixture(scope="module")
def encoders():
    return R.parity_encoders(["MiniPokeV1_1M", "MiniPokeV2_1_1M"])


# --------------------------------------------------------------------------
# Filters and records
# --------------------------------------------------------------------------
def test_battle_lines_drop_the_rooms_own_lines():
    lines = [
        "|init|battle", "|title|a vs. b", "|j|☆a", "|badge|p1|bronze|gen9randombattle|100-11",
        "|player|p1|a|1|1500", "|player|p2|b|2|1600", "|gametype|singles", "|t:|1",
        "|start", "|switch|p1a: Mew|Mew, L80|100/100", "|turn|1",
        "|request|{\"rqid\":2}", "|inactive|a has 120 seconds left.", "|c|☆a|gl",
        "'rock solid' has no exact match.", "|error|'paralyzed' doesn't match anything",
        "|player|p2|b|2|", "|player|p1|", "|", "|-message|b forfeited.",
        "|-message|Sleep Clause Mod activated.", "|win|a",
        "||Ladder isn't responding, score probably updated", "|raw|a's rating: 1500", "",
        "|-hint|If you want to tie earlier, consider using `/offertie`.",
    ]
    assert R.battle_lines(lines) == [
        "|player|p1|a|1|1500", "|player|p2|b|2|1600", "|gametype|singles", "|start",
        "|switch|p1a: Mew|Mew, L80|100/100", "|turn|1", "|request|{\"rqid\":2}",
        "|error|'paralyzed' doesn't match anything", "|", "|-message|Sleep Clause Mod activated.",
        "|win|a"]
    without = R.battle_lines(lines, requests=False)
    assert not any(line.startswith(("|request|", "|error|")) for line in without)


def test_first_difference():
    assert R.first_difference(["|a", "|b"], ["|a", "|b"]) is None
    assert R.first_difference(["|a", "|b"], ["|a", "|c"]).startswith("line 1")
    assert R.first_difference(["|a"], ["|a", "|b"]) == "lengths 1 != 2"


def test_answered_choices_match_decisions_to_requests_by_line():
    requests = [{"line": 3, "wait": False, "choice": "move tackle"},
                {"line": 7, "wait": True, "choice": None},
                {"line": 9, "wait": False, "choice": None}]
    assert R.answered_choices([{"line": 3}, {"line": 9}], requests) == (["move tackle", None], 0)
    assert R.answered_choices([{"line": 7}, {"line": 5}], requests)[1] == 2   # a wait, no request


def test_judge_verdicts():
    replay = R.Replay("x", Path("x"), 0, "abc", "", ["|start", "|win|a", ""], {})
    ok = {"ok": True, "ended": True, "choice_errors": [], "omniscient": ["|start", "|t:|5", "|win|a"]}
    assert R.judge(replay, ok).status == "ok"
    assert R.judge(replay, {**ok, "omniscient": ["|start", "|win|b"]}).status == "mismatch"
    assert R.judge(replay, {**ok, "ended": False}).status == "not_ended"
    assert R.judge(replay, {**ok, "choice_errors": [">p1 move x: refused"]}).status == "choice_errors"
    assert R.judge(replay, {"ok": False, "error": "boom"}).status == "error"


def test_replay_files_keep_one_file_per_id(tmp_path):
    for folder in ("a/2026-11-01", "b"):
        (tmp_path / folder).mkdir(parents=True)
    for path in ("a/2026-11-01/gen9randombattle-1.json.gz", "a/2026-11-01/gen9randombattle-2.json.gz",
                 "b/gen9randombattle-2.json.gz", "b/state.json"):
        (tmp_path / path).write_bytes(b"")
    files, dupes = R.replay_files([tmp_path / "a", tmp_path / "b"])
    assert [f.name for f in files] == ["gen9randombattle-1.json.gz", "gen9randombattle-2.json.gz"]
    assert dupes == 1


def test_fixtures_are_anonymized_and_have_no_chat():
    assert len(STEMS) == 3
    for stem in STEMS:
        text = (FIXTURES / f"{stem}.log").read_text() + (FIXTURES / f"{stem}.json").read_text()
        assert "Elsa-Mina" not in text and "|c|" not in text and ">chat" not in text
        assert "Player1" in text and "Player2" in text


# --------------------------------------------------------------------------
# Parity: what the bot received live, against the re-simulation
# --------------------------------------------------------------------------
@pytest.mark.parametrize("stem", STEMS)
def test_the_resimulation_is_what_the_bot_received(stem, encoders):
    """The bot's side of the re-simulation: the .log's battle lines exactly, then the same
    decision points, every V1 and V2.1 array equal, and the same action for every choice."""
    lines = A.read_lines(FIXTURES / f"{stem}.log")
    result = R.parity_battle(stem, lines, resim_of(stem, stored(stem)), encoders)
    assert result.status == "same", result.detail
    assert result.decisions > 5 and result.actions >= result.decisions - 1
    assert result.arrays == result.decisions * sum(len(e.layout) for e in encoders.values())


def test_parity_notices_a_changed_line(encoders):
    stem = STEMS[0]
    lines = A.read_lines(FIXTURES / f"{stem}.log")
    out = stored(stem)
    side = A.parse_log(lines).our_side
    i = next(k for k, line in enumerate(out["sides"][side]) if line.startswith("|-damage|"))
    out["sides"][side][i] = out["sides"][side][i].replace("|-damage|", "|-heal|")
    assert R.parity_battle(stem, lines, resim_of(stem, out), encoders).status == "lines"


def test_both_sides_decode_and_every_answered_choice_is_legal(encoders):
    """What human_data.py trains on: both sides of a re-simulation, each choice mapped to an
    action legal in poke-env's mask."""
    from analyze_replays import choice_to_action
    stem = STEMS[1]
    res = resim_of(stem, stored(stem))
    for side in R.SIDES:
        dec = R.side_decisions(res.sides[side], res.replay.name(side), encoders, stem)
        choices, strays = R.answered_choices(dec["decisions"], res.requests[side])
        assert strays == 0 and not dec["parse_errors"] and dec["decisions"]
        for d, text in zip(dec["decisions"], choices):
            if text is None:
                continue
            action, problem = choice_to_action(R.as_choice(side, text), d["team"], d["request_team"], d["moves"])
            assert action is not None, problem
            assert np.asarray(d["obs"]["MiniPokeV2_1_1M"]["mask"])[action]


NODE = shutil.which("node") is not None and ShowdownVersions().is_built(VERSION)


@pytest.mark.skipif(not NODE, reason=f"needs Node and Showdown {VERSION} (python resim.py parity "
                                     f"tests/fixtures/resim builds it)")
@pytest.mark.parametrize("stem", STEMS)
def test_resim_js_reproduces_the_stored_resimulation(stem):
    replay = R.read_replay(FIXTURES / f"{stem}.json")
    worker = R.ResimWorker(ShowdownVersions().path(VERSION))
    try:
        out = worker.resim(stem, replay.inputlog)
    finally:
        worker.close()
    expected = stored(stem)
    for side in R.SIDES:   # the |t:| lines are the time the simulator ran
        assert R.battle_lines(out["sides"][side]) == R.battle_lines(expected["sides"][side])
    assert out["requests"] == expected["requests"]
    assert R.judge(replay, out).status == "ok"   # its omniscient view is the public log


def test_showdown_versions_records_failures_and_reuses_builds(tmp_path):
    calls = []

    def runner(cmd, cwd=None, timeout=0):
        calls.append(cmd)
        raise BuildFailed("no network")
    versions = ShowdownVersions(tmp_path, runner=runner)
    with pytest.raises(BuildFailed):
        versions.ensure("0123456789abcdef")
    assert "0123456789ab" in versions.failures()
    n = len(calls)
    with pytest.raises(BuildFailed, match="no network"):
        versions.ensure("0123456789abcdef")      # recorded: not tried again
    assert len(calls) == n
    built = versions.path("fedcba9876543210")
    built.mkdir()
    (built / ".minipoke-built").write_text("{}")
    assert versions.ensure("fedcba9876543210") == built
