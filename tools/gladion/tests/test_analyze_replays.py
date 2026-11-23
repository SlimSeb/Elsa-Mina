"""analyze_replays.py on small hand-made logs: parsing, choices -> actions, parity."""

import json

import numpy as np
import pytest

import analyze_replays as A
from tests.battles import mon, request
from tests.priors import hand_priors

STEM = "gen9randombattle-1"


def team(active: str = "Garchomp", **hp) -> list:
    out = [
        mon("Garchomp", ["Earthquake", "Outrage", "Stealth Rock", "Swords Dance"], level=77,
            ability="roughskin", tera="Ground"),
        mon("Rotom-Wash", ["Hydro Pump", "Volt Switch", "Will-O-Wisp", "Pain Split"], level=86,
            ability="levitate", tera="Electric"),
        mon("Corviknight", ["Brave Bird", "Roost", "Defog", "U-turn"], level=81,
            ability="mirrorarmor", tera="Dragon"),
    ]
    for m in out:
        m["active"] = m["ident"] == f"p1: {active}"
        species = m["ident"][4:]
        if species in hp:
            m["condition"] = hp[species]
    # Showdown lists the active Pokémon first
    return sorted(out, key=lambda m: not m["active"])


def req(t: list, **kwargs) -> str:
    return "|request|" + json.dumps(request(t, **kwargs))


def trapped_maybe(t: list) -> str:
    r = request(t)
    r["active"][0]["maybeTrapped"] = True
    return "|request|" + json.dumps(r)


def wait(t: list) -> str:
    return "|request|" + json.dumps({"wait": True, "side": {"name": "me", "id": "p1", "pokemon": t},
                                     "rqid": 9})


def battle_log() -> list:
    """Turn 1: Earthquake. Turn 2: a switch Showdown rejects (trapped), then
    Earthquake KOs Venusaur; their replacement; turn 3: they forfeit before we
    choose. Decision points: 4 (the wait request is not one)."""
    return [
        "|init|battle", "|title|me vs. them", "|gametype|singles",
        "|player|p1|me|1|1100", "|player|p2|them|2|1050", "|teamsize|p1|3", "|teamsize|p2|2",
        "|gen|9", "|tier|[Gen 9] Random Battle", "|rated|", "|", "|t:|1700000000", "|start",
        "|switch|p1a: Garchomp|Garchomp, L77|250/250", "|switch|p2a: Venusaur|Venusaur, L84|100/100",
        "|turn|1", req(team()),
        "|", "|t:|1700000010",
        "|move|p1a: Garchomp|Earthquake|p2a: Venusaur", "|-resisted|p2a: Venusaur",
        "|-damage|p2a: Venusaur|60/100",
        "|move|p2a: Venusaur|Giga Drain|p1a: Garchomp", "|-damage|p1a: Garchomp|200/250",
        "|", "|upkeep", "|turn|2", trapped_maybe(team(Garchomp="200/250")),
        "|error|[Unavailable choice] Can't switch: The active Pokémon is trapped",
        req(team(Garchomp="200/250"), trapped=True),
        "|", "|t:|1700000020",
        "|move|p1a: Garchomp|Earthquake|p2a: Venusaur", "|-damage|p2a: Venusaur|0 fnt",
        "|faint|p2a: Venusaur", "|", "|upkeep", wait(team(Garchomp="200/250")),
        "|", "|t:|1700000030", "|switch|p2a: Corviknight|Corviknight, L81|100/100",
        "|turn|3", req(team(Garchomp="200/250")),
        "|-message|them forfeited.", "|", "|win|me",
    ]


ERROR_LINE = next(i for i, line in enumerate(battle_log()) if line.startswith("|error|"))


def as_choice(action: int, decision: dict) -> str:
    """The inputlog choice for a compact action at this decision point."""
    if action < 6:
        return f"switch {decision['request_team'].index(decision['team'][action]) + 1}"
    return f"move {decision['moves'][(action - 6) % 4]}" + (" terastallize" if action >= 10 else "")


def replay_json(our_choices: list, rated: bool = True) -> dict:
    inputlog = [">version abc", '>start {"formatid":"gen9randombattle"}',
                '>player p1 {"name":"me","rating":1100}', '>player p2 {"name":"them","rating":1050}']
    theirs = ["move gigadrain", "move sleeppowder", "switch 2"]
    for i, ours in enumerate(our_choices):
        inputlog += [f">p1 {ours}", f">p2 {theirs[i]}"]
    inputlog += [f">p2 {theirs[-1]}", ">forcelose p2"]
    log = "\n".join(battle_log()[:-1] + ["|win|me"])
    if rated:
        log += ("\n|raw|them's rating: 1050 &rarr; <strong>1035</strong><br />(-15 for losing)"
                "\n|raw|me's rating: 1100 &rarr; <strong>1115</strong><br />(+15 for winning)")
    return {"id": STEM, "formatid": "gen9randombattle", "uploadtime": 1700000100,
            "players": ["me", "them"], "rating": 1100 if rated else 0, "log": log,
            "inputlog": "\n".join(inputlog)}


# --------------------------------------------------------------------------
# Parsing
# --------------------------------------------------------------------------
def test_parse_choice_forms():
    move = A.parse_choice(">p2 move knockoff terastallize")
    assert (move.side, move.kind, move.move, move.tera) == ("p2", "move", "knockoff", True)
    switch = A.parse_choice(">p1 switch 3")
    assert (switch.kind, switch.position) == ("switch", 3)
    assert A.parse_choice(">player p1 {}") is None and A.parse_choice(">chat hi") is None


def test_parse_log_reads_sides_ratings_and_ending():
    info = A.parse_log(battle_log())
    assert (info.our_side, info.our_name, info.opp_side) == ("p1", "me", "p2")
    assert info.names == {"p1": "me", "p2": "them"}
    assert info.ratings == {"p1": 1100, "p2": 1050}
    assert (info.turns, info.start_time, info.end_time) == (3, 1700000000, 1700000030)
    assert info.outcome == "win"
    assert info.ending == ("forfeit", "p2")
    assert (info.mons_left("p1"), info.mons_left("p2")) == (3, 1)
    assert info.errors == [(ERROR_LINE, "[Unavailable choice] Can't switch: The active Pokémon is trapped")]
    assert info.requests == 5


def test_parse_log_endings_without_forfeit():
    lines = battle_log()
    idle = A.parse_log([l for l in lines if not l.startswith("|-message|")] + ["|-message|them lost due to inactivity."])
    assert idle.ending == ("inactivity", "p2")
    fainted = A.parse_log(["|player|p1|me|1|", "|player|p2|them|2|", "|teamsize|p1|1", "|teamsize|p2|1",
                           "|faint|p1a: Garchomp", "|win|them"])
    assert fainted.ending == ("all fainted", "p1") and fainted.ratings == {}
    assert A.parse_log(["|tie"]).ending == ("tie", None)


def test_parse_replay_reads_choices_and_rating_changes():
    info = A.parse_replay(replay_json(["move earthquake", "move earthquake"]))
    assert [c.raw for c in info.choices_of("p1")] == ["p1 move earthquake", "p1 move earthquake"]
    assert [c.kind for c in info.choices_of("p2")] == ["move", "move", "switch"]
    assert info.forcelose == ["p2"]
    assert info.players["p1"]["rating"] == 1100
    assert info.rating_changes == {"them": (1050, 1035), "me": (1100, 1115)}


def test_pairing_reports_unpaired_and_broken_files(tmp_path):
    (tmp_path / f"{STEM}.log").write_text("\n".join(battle_log()) + "\n")
    (tmp_path / f"{STEM}.json").write_text(json.dumps(replay_json(["move earthquake"])))
    (tmp_path / "gen9randombattle-2.log").write_text("|init|battle\n")
    (tmp_path / "gen9randombattle-3.log").write_text("|init|battle\n")
    (tmp_path / "gen9randombattle-3.json").write_text("{not json")
    stems, issues = A.pair_files(tmp_path)
    assert stems == [STEM, "gen9randombattle-3"]
    assert issues == ["`gen9randombattle-2.log` has no `.json`"]
    with pytest.raises(A.BrokenPair, match="unreadable .json"):
        A.load_pair(tmp_path, "gen9randombattle-3")
    lines, log, replay = A.load_pair(tmp_path, STEM)
    assert len(lines) == len(battle_log()) and log.our_side == "p1" and replay.id == STEM


# --------------------------------------------------------------------------
# Choices -> actions
# --------------------------------------------------------------------------
def test_choice_to_action_uses_the_request_order_for_switches():
    battle_team = ["p1: Garchomp", "p1: Rotom-Wash", "p1: Corviknight"]
    # after a switch Showdown lists the new active Pokémon first
    request_team = ["p1: Corviknight", "p1: Garchomp", "p1: Rotom-Wash"]
    moves = ["bravebird", "roost", "defog", "uturn"]
    action = lambda raw: A.choice_to_action(A.parse_choice(raw), battle_team, request_team, moves)
    assert action(">p1 switch 2") == (0, None)        # Garchomp, team slot 0
    assert action(">p1 switch 3") == (1, None)        # Rotom-Wash, team slot 1
    assert action(">p1 move roost") == (7, None)
    assert action(">p1 move uturn terastallize") == (13, None)
    assert action(">p1 switch 4") == (None, "switch position outside the request's team")
    assert action(">p1 move outrage") == (None, "move not among poke-env's move actions")
    assert A.choice_to_action(A.parse_choice(">p1 move struggle"), battle_team, request_team,
                              ["struggle"]) == (6, None)
    assert A.choice_to_action(A.parse_choice(">p1 team 123"), battle_team, request_team, moves)[0] is None


def test_align_choices_skips_rejected_and_uncommitted_decisions():
    statuses, index, leftover = A.align_choices([10, 20, 30, 40], error_lines=[25], n_choices=2)
    assert statuses == ["committed", "rejected", "committed", "uncommitted"]
    assert index == [0, None, 1, None] and leftover == 0
    assert A.align_choices([10], [], 3)[2] == 2   # more choices than decision points


def test_check_choice():
    mask = np.array([True] * 10 + [False] * 4)
    logits = np.zeros(14, np.float32)
    logits[7] = 2.0
    assert A.check_choice(logits, mask, 7) == "match"
    assert A.check_choice(logits, mask, 6) == "mismatch"
    assert A.check_choice(logits, mask, 11) == "illegal"
    assert A.check_choice(logits, mask, None) == "unmapped"
    logits[6] = 2.0                                    # argmax 6 (first), 7 ties with it
    assert A.check_choice(logits, mask, 7) == "tie"


def test_realized_returns_follow_the_training_reward():
    # one faint each way between decisions, then a win
    returns = A.realized_returns([[0, 0], [0, 1], [1, 1]], final=[1, 2], result=1.0)
    rewards = [0.1, -0.1, 0.1 + 1.0]
    assert returns[2] == pytest.approx(rewards[2])
    assert returns[0] == pytest.approx(rewards[0] + A.GAMMA * rewards[1] + A.GAMMA ** 2 * rewards[2])


# --------------------------------------------------------------------------
# Replay into poke-env
# --------------------------------------------------------------------------
@pytest.fixture(scope="module")
def replayed():
    from encoder_v2 import EncoderV2

    return A.replay_battle(STEM, battle_log(), "me", {"v2": EncoderV2(hand_priors(), history=True)})


def test_replay_finds_poke_envs_decision_points(replayed):
    decisions = replayed["decisions"]
    lines = battle_log()
    # every |request| but the wait one, at the request's own line
    assert [lines[d["line"]].startswith("|request|") for d in decisions] == [True] * 4
    assert [d["turn"] for d in decisions] == [1, 2, 2, 3]
    assert [d["trapped"] for d in decisions] == [False, False, True, False]
    assert decisions[0]["team"] == ["p1: Garchomp", "p1: Rotom-Wash", "p1: Corviknight"]
    assert decisions[0]["moves"] == ["earthquake", "outrage", "stealthrock", "swordsdance"]
    assert replayed["obs"]["v2"]["mask"].shape == (4, 14)
    assert replayed["parse_errors"] == [] and replayed["final_fainted"] == [0, 1]
    assert not replayed["obs"]["v2"]["mask"][2][:6].any()   # trapped: no switch


def test_replay_cache_round_trip(tmp_path, replayed):
    path = tmp_path / f"{STEM}.npz"
    A.save_replay(path, "sha", replayed)
    loaded = A.load_replay(path, "sha", ["v2"])
    assert loaded["decisions"][1]["line"] == replayed["decisions"][1]["line"]
    np.testing.assert_array_equal(loaded["obs"]["v2"]["cand_ids"], replayed["obs"]["v2"]["cand_ids"])
    assert A.load_replay(path, "another log", ["v2"]) is None
    assert A.load_replay(path, "sha", ["v2", "another encoder"]) is None


def test_behavior_scan():
    lines = ["|switch|p1a: Garchomp|Garchomp, L77|250/250", "|switch|p2a: Rotom|Rotom-Wash, L86|100/100",
             "|turn|1", "|move|p1a: Garchomp|Earthquake|p2a: Rotom", "|-immune|p2a: Rotom|[from] ability: Levitate",
             "|move|p2a: Rotom|Will-O-Wisp|p1a: Garchomp", "|-status|p1a: Garchomp|brn",
             "|turn|2", "|switch|p2a: Venusaur|Venusaur, L84|100/100",           # voluntary
             "|move|p1a: Garchomp|Swords Dance|p1a: Garchomp", "|-damage|p1a: Garchomp|50/250 brn",
             "|turn|3", "|-terastallize|p1a: Garchomp|Ground",
             "|move|p1a: Garchomp|Swords Dance|p1a: Garchomp", "|move|p2a: Venusaur|Toxic|p1a: Garchomp",
             "|-fail|p1a: Garchomp", "|turn|4",
             "|switch|p2a: Rotom|Rotom-Wash, L86|100/100", "|move|p1a: Garchomp|Earthquake|p2a: Rotom",
             "|-immune|p2a: Rotom|[from] ability: Levitate", "|turn|5"]
    sides = A.scan_behavior(lines)
    us, them = sides["p1"], sides["p2"]
    assert (us.immune, us.immune_ability, us.immune_repeated) == (2, 2, 1)
    assert (us.setup_low_hp, us.setup_low_hp_repeated) == (1, 1)         # turn 3 only: 20% after SD
    assert them.status_into_status == 1 and them.failed == 1 and them.failed_moves == {"toxic": 1}
    assert (them.voluntary_switches, us.voluntary_switches) == (2, 0)
    assert us.turn_choices == them.turn_choices == 4
    assert us.tera_turn == 3 and them.tera_turn is None


# --------------------------------------------------------------------------
# Parity, with the model ElsaMina plays
# --------------------------------------------------------------------------
@pytest.fixture(scope="module")
def model():
    return A.load_model(A.PLAYED_BY)


def test_parity_check_on_a_replayed_log(model):
    replayed = A.replay_battle(STEM, battle_log(), "me", {model.key: model.encoder})
    obs = replayed["obs"][model.key]
    logits, _ = A.evaluate(model, obs)
    _, argmax, _ = A.policy(logits, obs["mask"].astype(bool))
    decisions = replayed["decisions"]
    # the bot's committed choices: what the argmax says at decisions 0 and 2
    # (decision 1 was rejected, decision 3 never committed)
    chosen = [as_choice(int(argmax[k]), decisions[k]) for k in (0, 2)]
    assert not chosen[1].startswith("switch")   # trapped
    ours = A.parse_replay(replay_json(chosen)).choices_of("p1")
    statuses, index, _ = A.align_choices([d["line"] for d in decisions], [ERROR_LINE], len(ours))
    assert statuses == ["committed", "rejected", "committed", "uncommitted"]
    for k in (0, 2):
        action, problem = A.choice_to_action(ours[index[k]], decisions[k]["team"],
                                             decisions[k]["request_team"], decisions[k]["moves"])
        assert problem is None
        assert A.check_choice(logits[k], obs["mask"][k], action) == "match"
        legal = np.flatnonzero(obs["mask"][k])
        worst = int(legal[np.argmin(logits[k][legal])])
        assert A.check_choice(logits[k], obs["mask"][k], worst) == "mismatch"


def attributed_battle(start: int, picks: list, vetoes: dict = None) -> A.Battle:
    """Decisions where model "one" plays move 6 (then 8) and "four" plays 7; `vetoes`
    is what ElsaMina's guard vetoes at each decision."""
    log = A.LogInfo(our_side="p1", start_time=start)
    decisions = []
    for action in picks:
        mask = np.ones(14, bool)
        models = {}
        for name, logits in {"one": np.eye(14)[6] + 0.5 * np.eye(14)[8], "four": np.eye(14)[7]}.items():
            own = {"logits": logits}
            own["guarded"], own["guarded_mask"] = A.guarded(logits, mask, vetoes or {})
            own["candidates"] = A.search_candidates(logits, own["guarded_mask"])
            models[name] = own
        decisions.append({"status": "committed", "choice": "move x", "action": action, "mask": mask,
                          "models": models})
    return A.Battle(str(start), [], log, A.ReplayInfo("", "", None, None, {}, [], [], {}), decisions, [], [], [0, 0])


def test_attribution_picks_the_model_that_explains_the_choices():
    battles = [attributed_battle(100, [6, 6]), attributed_battle(200, []), attributed_battle(300, [7, 7, 6]),
               attributed_battle(310, [])]
    A.attribute_models(battles, ["one", "four"])
    assert [b.played_by for b in battles] == ["one", "one", "four", "four"]
    assert [b.attribution for b in battles] == ["choices", "nearest in time", "choices", "nearest in time"]
    assert battles[2].model_matches == {"one": 1, "four": 2}
    assert not any(b.guard for b in battles)


def test_attribution_tells_whether_the_guard_played():
    vetoed = {6: "immune", 10: "immune"}
    battles = [attributed_battle(100, [8, 8], vetoed),    # "one" with the guard: 6 is vetoed, 8 next best
               attributed_battle(150, [6, 6]),            # nothing vetoed: can't tell, nearest is 100
               attributed_battle(400, [6, 6], vetoed)]    # vetoed, but the bot played it: no guard
    A.attribute_models(battles, ["one", "four"])
    assert [b.played_by for b in battles] == ["one", "one", "one"]
    assert [b.guard for b in battles] == [True, True, False]
    assert [b.guard_attribution for b in battles] == ["choices", "nearest in time", "choices"]
    assert battles[0].guard_matches == {"without": 0, "with": 2}


def test_attribution_tells_whether_the_search_played():
    # "one" plays 6, then 8, then the first of the 12 others: its candidates are 6, 8 and 0
    battles = [attributed_battle(100, [6, 8, 6]),     # 8 is a search candidate, not the argmax
               attributed_battle(150, [6, 6]),        # can't tell: nearest is 100
               attributed_battle(400, [6, 7, 6])]     # 7 (only "four" plays it) is no candidate of "one"
    A.attribute_models(battles, ["one", "four"])
    assert [b.search for b in battles] == [True, True, False]
    assert [b.search_attribution for b in battles] == ["choices", "nearest in time", "choices"]


def test_search_candidates_are_the_likely_allowed_actions():
    logits = np.array([0.0] * 6 + [3.0, 2.0, 1.0, -5.0] + [0.0] * 4)
    mask = np.zeros(14, bool)
    mask[6:10] = True
    assert A.search_candidates(logits, mask) == {6, 7, 8}
    mask[7] = False
    assert A.search_candidates(logits, mask) == {6, 8, 9}    # the top 3, however unlikely
    assert A.search_candidates(logits, np.eye(14, dtype=bool)[6]) == set()


GUARD_REQUEST = {"side": {"id": "p1", "pokemon": [
    {"ident": "p1: Sandaconda", "active": True, "condition": "258/258", "ability": "shedskin"}]}}


def guard(lines, moves=("earthquake", "stealthrock", "recover", "sleeptalk"), request=None):
    return A.guard_vetoes(["|switch|p1a: Sandaconda|Sandaconda, L84|258/258",
                           "|switch|p2a: Eelektross|Eelektross, L87|100/100", *lines],
                          "p1", request or GUARD_REQUEST, moves)


def test_guard_vetoes_moves_that_cannot_work():
    immune = ["|turn|2", "|move|p1a: Sandaconda|Earthquake|p2a: Eelektross",
              "|-immune|p2a: Eelektross|[from] ability: Levitate", "|turn|3"]
    assert guard(immune) == {6: "immune", 10: "immune", 8: "heal", 12: "heal", 9: "asleep", 13: "asleep"}
    # a tera since, another target, or Showdown's immunity of another side: no veto
    assert 6 not in guard(immune + ["|-terastallize|p2a: Eelektross|Ground"])
    assert 6 not in guard(immune + ["|switch|p2a: Rotom|Rotom, L88|100/100"])
    assert 6 in guard(immune + ["|switch|p2a: Rotom|Rotom, L88|100/100",
                                "|switch|p2a: Eelektross|Eelektross, L87|100/100"])
    hurt = {"side": {"pokemon": [{"active": True, "condition": "100/258 slp", "ability": "shedskin"}]}}
    assert guard([], request=hurt) == {}


def test_guard_counts_hazard_layers():
    rocks = ["|-sidestart|p2: Them|move: Stealth Rock"]
    assert guard(rocks)[7] == "hazard"
    assert 7 not in guard(rocks + ["|-sideend|p2: Them|Stealth Rock|[from] move: Defog"])
    assert 7 not in guard(rocks + ["|-swapsideconditions|"])
    spikes = ["|-sidestart|p2: Them|Spikes"] * 2
    assert 7 not in guard(spikes, moves=("earthquake", "spikes"))
    assert guard(spikes * 2, moves=("earthquake", "spikes"))[7] == "hazard"


def test_guard_remembers_a_hidden_trap_until_an_active_changes():
    error = ["|error|" + A.TRAPPED_ERROR]
    assert all(guard(error)[slot] == "trapped" for slot in range(6))
    assert 0 not in guard(error + ["|switch|p2a: Rotom|Rotom, L88|100/100"])
    forced = {**GUARD_REQUEST, "forceSwitch": [True]}
    assert 0 not in guard(error, request=forced)


def test_guarded_plays_the_best_action_left():
    logits, mask = np.arange(14, dtype=float), np.ones(14, bool)
    assert A.guarded(logits, mask, {})[0] == 13
    assert A.guarded(logits, mask, {13: "immune", 9: "immune"})[0] == 12
    only = np.zeros(14, bool)
    only[13] = True
    assert A.guarded(logits, only, {13: "immune"})[0] == 13   # nothing left: the model's choice


def test_calibration_fits_the_realized_returns():
    values = np.array([-0.5, 0.0, 0.5, 1.0])
    fit = A.calibrate(values, 0.5 * values - 0.25)
    assert fit["scale"] == pytest.approx(0.5) and fit["offset"] == pytest.approx(-0.25)
    assert fit["mean_gap_after"] == pytest.approx(0.0, abs=1e-12)


def test_whole_report_on_a_tiny_folder(tmp_path, model):
    replays = tmp_path / "replays"
    replays.mkdir()
    (replays / f"{STEM}.log").write_text("\n".join(battle_log()) + "\n")
    (replays / f"{STEM}.json").write_text(json.dumps(replay_json(["move earthquake", "move earthquake"])))
    (replays / "gen9randombattle-2.json").write_text("{}")
    out = tmp_path / "analysis"
    result = A.analyze(replays, out, refresh=False, compare=False, limit=None)
    assert result["pairing_issues"] == ["`gen9randombattle-2.json` has no `.log`"]
    battle, = result["battles"]
    assert [d["status"] for d in battle.decisions] == ["committed", "rejected", "committed", "uncommitted"]
    assert all(d["check"] in ("match", "tie", "mismatch") for d in battle.decisions if d["status"] == "committed")
    report = A.write_outputs(result, out).read_text()
    assert "## Parity" in report and chr(0x2014) not in report   # no em dash
    assert (out / "battles.csv").exists() and (out / "decisions.jsonl").read_text().count("\n") == 4
    # a rerun reads the cache
    assert A.analyze(replays, out, refresh=False, compare=False, limit=None)["battles"][0].decisions[0]["line"] == 16
