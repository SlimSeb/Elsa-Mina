"""search.py: the position sent to Showdown's simulator, leaf scoring and the turn's game."""

import asyncio
import shutil
from pathlib import Path

import numpy as np
import pytest

from encoder_v2 import EncoderV1
from search import (SearchConfig, SearchPlayer, SimPool, leaf_battle, leaf_candidates, position_of, sim_action,
                    solve_zero_sum)
from tests.battles import default_team, make_battle, opponent_switch

SHOWDOWN = Path.home() / "Documents/minipoke/pokemon-showdown"
ROTOM = opponent_switch("Rotom-Wash")


def test_regret_matching_finds_the_equilibrium():
    pennies = np.array([[1.0, -1.0], [-1.0, 1.0]])
    ours, theirs = solve_zero_sum(pennies, 2000)
    assert ours == pytest.approx([0.5, 0.5], abs=0.03) and theirs == pytest.approx([0.5, 0.5], abs=0.03)
    dominated = np.array([[1.0, 2.0], [0.0, 0.5]])
    ours, theirs = solve_zero_sum(dominated, 2000)
    assert ours[0] > 0.97 and theirs[0] > 0.97


def test_position_holds_our_request_and_what_the_opponent_revealed():
    battle = make_battle(default_team(), [ROTOM, "|turn|1", "|move|p2a: Rotom-Wash|Hydro Pump|p1a: Garchomp",
                                          "|-damage|p1a: Garchomp|100/250", "|-sidestart|p1: me|move: Stealth Rock",
                                          "|turn|2"])
    pos = position_of(battle)
    ours, theirs = pos["sides"]["p1"], pos["sides"]["p2"]
    garchomp = ours["pokemon"][0]
    assert (pos["us"], pos["turn"]) == ("p1", 2)
    assert garchomp["name"] == "Garchomp" and garchomp["active"] and garchomp["maxhp"] == 250
    assert garchomp["stats"]["spe"] == 190 and garchomp["moves"][0] == {"id": "earthquake", "pp": 8}
    assert ours["conditions"] == {"stealthrock": 1} and ours["size"] == 3
    rotom, = theirs["pokemon"]
    assert rotom["name"] == "Rotom-Wash" and rotom["moves"] == [{"id": "hydropump", "pp": 7}]
    assert rotom["item"] is None and rotom["hpFraction"] == 1.0 and theirs["size"] == 6


def test_sim_actions_name_switches_and_moves():
    battle = make_battle(default_team(), [ROTOM, "|turn|1"])
    assert sim_action(1, battle) == {"kind": "switch", "name": "Rotom-Wash"}
    assert sim_action(6, battle) == {"kind": "move", "move": "earthquake", "tera": False}
    assert sim_action(13, battle) == {"kind": "move", "move": "swordsdance", "tera": True}


def test_leaf_candidates_are_the_likeliest_actions_showdown_allows():
    leaf = make_battle(default_team(), [ROTOM, "|turn|1"])
    probs = np.zeros(14)
    probs[[1, 2, 6, 7, 10]] = [0.1, 0.05, 0.2, 0.4, 0.25]   # Rotom, Corviknight, EQ, Outrage, tera EQ
    choices = {"wait": False, "moves": ["earthquake", "stealthrock"], "tera": False, "switches": ["Corviknight"]}
    # Outrage isn't allowed (Showdown's choices at the leaf), tera EQ neither, nor Rotom (fainted, say)
    assert leaf_candidates(leaf, choices, probs, 2) == [{"kind": "move", "move": "earthquake", "tera": False},
                                                        {"kind": "switch", "name": "Corviknight"}]
    assert leaf_candidates(leaf, {**choices, "tera": True}, probs, 2) == [
        {"kind": "move", "move": "earthquake", "tera": True}, {"kind": "move", "move": "earthquake", "tera": False}]
    assert leaf_candidates(leaf, {"wait": True}, probs, 2) == []
    assert leaf_candidates(leaf, {"wait": False, "moves": ["struggle"], "tera": False, "switches": []},
                           probs, 2) is None


def test_a_leaf_is_a_copy_of_the_battle_after_the_simulated_turn():
    battle = make_battle(default_team(), [ROTOM, "|turn|1"])
    leaf = leaf_battle(battle, ["|move|p2a: Rotom-Wash|Hydro Pump|p1a: Garchomp",
                                "|-damage|p1a: Garchomp|0 fnt", "|faint|p1a: Garchomp", "|upkeep", "|turn|2"])
    assert leaf.turn == 2 and leaf.active_pokemon.fainted and leaf.force_switch
    assert battle.turn == 1 and not battle.active_pokemon.fainted


class FakeBatcher:
    """Policy: Earthquake first. Value: the opponent's HP fraction, negated."""
    encoder = EncoderV1()

    async def act(self, obs, greedy=False):
        probs = np.zeros(14)
        probs[6], probs[7] = 0.7, 0.3
        hp = obs["mon_feats"][6, 5]
        return 6, 0.0, float(-hp), probs


class FakeSims:
    """Earthquake does nothing (Levitate), Outrage halves the opponent."""

    async def simulate(self, request):
        assert [a["move"] for a in request["ours"]] == ["earthquake", "outrage"]   # top_k 2
        end = ["|upkeep", "|turn|2"]
        leaves = [[[{"lines": ["|move|p1a: Garchomp|Earthquake|p2a: Rotom-Wash",
                               "|-immune|p2a: Rotom-Wash|[from] ability: Levitate", *end], "winner": None}]],
                  [[{"lines": ["|move|p1a: Garchomp|Outrage|p2a: Rotom-Wash",
                               "|-damage|p2a: Rotom-Wash|50/100", *end], "winner": None}]]]
        return {"determinizations": [{"theirs": ["hydropump"], "leaves": leaves}]}


def test_search_plays_the_candidate_with_the_best_simulated_turn():
    battle = make_battle(default_team(), [ROTOM, "|turn|1"])
    player = SearchPlayer(FakeBatcher(), FakeSims(), SearchConfig(top_k=2), start_listening=False)
    order = asyncio.run(player._choose(battle))
    assert "outrage" in order.message
    assert (player.stats.searched, player.stats.changed) == (1, 1)


END = ["|upkeep", "|turn|2"]
EARTHQUAKE = ["|move|p1a: Garchomp|Earthquake|p2a: Rotom-Wash", "|-immune|p2a: Rotom-Wash|[from] ability: Levitate",
              *END]
OUTRAGE = ["|move|p1a: Garchomp|Outrage|p2a: Rotom-Wash", "|-damage|p2a: Rotom-Wash|50/100", *END]
CHOICES = {"wait": False, "moves": ["earthquake", "outrage"], "tera": False, "switches": []}


class FakeDeepSims:
    """Turn 1 as FakeSims (Outrage looks better). Turn 2: after Earthquake, Rotom-Wash goes
    down (we win); after Outrage, it heals back to full."""

    def __init__(self, first_lines_match: bool = True):
        self.expanded = []
        self.first_lines_match = first_lines_match

    async def simulate(self, request):
        if "expand" not in request:
            leaves = [[[{"lines": EARTHQUAKE, "winner": None, "choices": CHOICES}]],
                      [[{"lines": OUTRAGE, "winner": None, "choices": CHOICES}]]]
            return {"determinizations": [{"theirs": ["hydropump"], "leaves": leaves}]}
        self.expanded = request["expand"]
        expansions = []
        for item in request["expand"]:
            first = EARTHQUAKE if item["a"] == 0 else OUTRAGE
            if item["a"] == 0:
                leaf = {"lines": ["|-damage|p2a: Rotom-Wash|0 fnt", "|faint|p2a: Rotom-Wash", "|win|me"],
                        "winner": "p1"}
            else:
                leaf = {"lines": ["|-heal|p2a: Rotom-Wash|100/100", "|upkeep", "|turn|3"], "winner": None}
            expansions.append({"first": first if self.first_lines_match else first[1:], "wait": False,
                               "theirs": ["hydropump"], "leaves": [[[leaf]] for _ in item["ours"]]})
        return {"expansions": expansions}


def test_depth_two_scores_the_likeliest_replies_with_the_next_decision():
    battle = make_battle(default_team(), [ROTOM, "|turn|1"])
    sims = FakeDeepSims()
    player = SearchPlayer(FakeBatcher(), sims, SearchConfig(top_k=2, depth=2), start_listening=False)
    order = asyncio.run(player._choose(battle))
    assert "earthquake" in order.message
    ours = [{"kind": "move", "move": "earthquake", "tera": False}, {"kind": "move", "move": "outrage", "tera": False}]
    assert sims.expanded == [{"d": 0, "a": 0, "b": 0, "s": 0, "ours": ours},
                             {"d": 0, "a": 1, "b": 0, "s": 0, "ours": ours}]
    assert player.stats.expanded == 2


def test_depth_two_keeps_a_leaf_whose_replayed_turn_differs():
    battle = make_battle(default_team(), [ROTOM, "|turn|1"])
    player = SearchPlayer(FakeBatcher(), FakeDeepSims(first_lines_match=False), SearchConfig(top_k=2, depth=2),
                          start_listening=False)
    order = asyncio.run(player._choose(battle))
    assert "outrage" in order.message and player.stats.expanded == 0


@pytest.mark.skipif(shutil.which("node") is None or not (SHOWDOWN / "dist").exists(),
                    reason="needs Node and the Showdown clone")
def test_showdown_plays_each_pair_of_actions_from_the_position():
    battle = make_battle(default_team(), [ROTOM, "|turn|1"])
    request = {"position": position_of(battle), "determinizations": 2, "samples": 1, "seed": 3,
               "ours": [sim_action(6, battle), sim_action(1, battle)]}

    async def run():
        sims = SimPool(SHOWDOWN, 1)
        try:
            return await sims.simulate(request)
        finally:
            await sims.aclose()

    reply = asyncio.run(run())
    det = reply["determinizations"][0]
    assert det["theirs"] and len(det["leaves"]) == 2 and len(det["leaves"][0]) == len(det["theirs"])
    earthquake = det["leaves"][0][0][0]["lines"]
    assert "|-immune|p2a: Rotom-Wash|[from] ability: Levitate" in earthquake
    assert any(line.startswith("|switch|p1a: Rotom-Wash") for line in det["leaves"][1][0][0]["lines"])


@pytest.mark.skipif(shutil.which("node") is None or not (SHOWDOWN / "dist").exists(),
                    reason="needs Node and the Showdown clone")
def test_showdown_plays_the_next_decision_from_a_replayed_leaf():
    battle = make_battle(default_team(), [ROTOM, "|turn|1"])
    request = {"position": position_of(battle), "determinizations": 1, "samples": 1, "seed": 5,
               "ours": [sim_action(7, battle), sim_action(1, battle)]}

    async def run():
        sims = SimPool(SHOWDOWN, 1)
        try:
            first = await sims.simulate(request)
            leaves = first["determinizations"][0]["leaves"]
            expand = [{"d": 0, "a": 0, "b": 0, "s": 0, "ours": [{"kind": "move", "move": "outrage", "tera": False}]},
                      {"d": 0, "a": 1, "b": 0, "s": 0, "ours": [{"kind": "switch", "name": "Garchomp"},
                                                                 {"kind": "move", "move": "hydropump", "tera": False}]}]
            return leaves, await sims.simulate({**request, "expand": expand})
        finally:
            await sims.aclose()

    leaves, reply = asyncio.run(run())
    outrage, switch = reply["expansions"]
    # Outrage locks Garchomp in: Showdown only offers it, and the next turn plays it again
    assert leaves[0][0][0]["choices"] == {"wait": False, "moves": ["outrage"], "tera": False, "switches": []}
    assert outrage["first"] == leaves[0][0][0]["lines"] and len(outrage["leaves"]) == 1
    assert any("|Outrage|" in line for line in outrage["leaves"][0][0][0]["lines"])
    # After switching to Rotom-Wash: back to Garchomp, or Hydro Pump
    assert switch["first"] == leaves[1][0][0]["lines"] and len(switch["leaves"]) == 2
    assert len(switch["leaves"][0]) == len(switch["theirs"])
    assert any(line.startswith("|switch|p1a: Garchomp") for line in switch["leaves"][0][0][0]["lines"])
    assert any("|Hydro Pump|" in line for line in switch["leaves"][1][0][0]["lines"])


@pytest.mark.skipif(shutil.which("node") is None or not (SHOWDOWN / "dist").exists(),
                    reason="needs Node and the Showdown clone")
def test_depth_two_search_on_showdown_deepens_leaves():
    battle = make_battle(default_team(), [ROTOM, "|turn|1"])

    async def run():
        sims = SimPool(SHOWDOWN, 1)
        try:
            player = SearchPlayer(FakeBatcher(), sims, SearchConfig(top_k=2, depth=2), start_listening=False)
            return player, await player._choose(battle)
        finally:
            await sims.aclose()

    player, order = asyncio.run(run())
    assert player.stats.failed == 0 and player.stats.searched == 1 and player.stats.expanded > 0
    assert "earthquake" in order.message or "outrage" in order.message
