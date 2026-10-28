"""search.py: the position sent to Showdown's simulator, leaf scoring and the turn's game."""

import asyncio
import shutil
from pathlib import Path

import numpy as np
import pytest

from encoder_v2 import EncoderV1
from search import SearchConfig, SearchPlayer, SimPool, leaf_battle, position_of, sim_action, solve_zero_sum
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
