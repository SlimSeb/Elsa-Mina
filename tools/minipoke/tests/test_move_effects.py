"""move_effects (effects.py): ability / item / type immunities and earlier move results."""

import asyncio

import numpy as np
import pytest
import torch

from effects import F_EFFECT
from encoder_v2 import EncoderV1, EncoderV2
from history import move_outcomes
from model import with_move_effects
from tests.battles import default_team, make_battle, opponent_switch
from tests.helpers import make_model, model_obs
from tests.priors import hand_priors

NO_EFFECT, ABILITY, STATUS_FAILS, IMMUNE_BEFORE, FAILED, FAILED_TWICE = range(F_EFFECT)
GARCHOMP, ROTOM = 0, 1                       # default_team() indexes; the active one leads
EARTHQUAKE, STEALTH_ROCK = 0, 2              # Garchomp's moves
HYDRO_PUMP, WILL_O_WISP = 0, 2               # Rotom-Wash's moves
ROTOM_LINE = "|switch|p2a: Rotom-Wash|Rotom-Wash, L86|100/100"


@pytest.fixture(scope="module")
def enc():
    return EncoderV2(hand_priors(), cand_moves=8, history=True, move_effects=True)


def split(*lines):
    return [line.split("|") for line in lines]


def effects_of(enc, team, lines):
    return enc.encode(make_battle(team, lines))


def test_outcomes_mark_failures_and_immunities_of_chosen_moves():
    out = move_outcomes(split(
        "|move|p1a: Garchomp|Earthquake|p2a: Rotom-Wash",
        "|-immune|p2a: Rotom-Wash|[from] ability: Levitate",
        "|turn|2",
        "|move|p1a: Garchomp|Stealth Rock|p2a: Rotom-Wash",
        "|-fail|p1a: Garchomp",
        "|move|p2a: Rotom-Wash|Will-O-Wisp|p1a: Garchomp",
        "|-immune|p2a: Rotom-Wash",                # its own side: not an immunity to its move
        "|turn|3",
        "|-fail|p1a: Garchomp",                    # after a boundary: nobody's
        "|move|p1a: Garchomp|Outrage|p2a: Rotom-Wash|[from]lockedmove",
        "|-fail|p1a: Garchomp",                    # a locked move is not a choice
        "|move|p1a: Garchomp|Stealth Rock|p2a: Rotom-Wash",
        "|-terastallize|p2a: Rotom-Wash|Fire",
    ))
    assert out.uses[("p1", "Garchomp", "stealthrock")] == [True, False]
    assert out.uses[("p1", "Garchomp", "earthquake")] == [False]
    assert out.uses[("p2", "Rotom-Wash", "willowisp")] == [False]
    assert ("p1", "Garchomp", "outrage") not in out.uses
    assert out.failed_last("p1", "Garchomp", "stealthrock", 1) is False
    assert out.immune == {("p1", "earthquake", "p2", "Rotom-Wash"): 1}
    assert not out.immune_before("p1", "earthquake", "p2", "Rotom-Wash")  # it terastallized since


def test_a_revealed_levitate_blocks_ground_moves_and_is_remembered(enc):
    lines = [ROTOM_LINE, "|turn|1", "|move|p1a: Garchomp|Earthquake|p2a: Rotom-Wash",
             "|-immune|p2a: Rotom-Wash|[from] ability: Levitate", "|turn|2"]
    feats = effects_of(enc, default_team(), lines)["effect_feats"]
    assert list(feats[GARCHOMP, EARTHQUAKE]) == [1, 1, 0, 1, 0, 0]
    assert not feats[GARCHOMP, 1].any()  # Outrage


def test_a_single_possible_ability_counts_before_it_is_revealed(enc):
    feats = effects_of(enc, default_team(), [ROTOM_LINE, "|turn|1"])["effect_feats"]
    assert list(feats[GARCHOMP, EARTHQUAKE]) == [1, 1, 0, 0, 0, 0]


def test_mold_breaker_ignores_the_ability(enc):
    team = default_team()
    team[GARCHOMP]["baseAbility"] = team[GARCHOMP]["ability"] = "moldbreaker"
    feats = effects_of(enc, team, [ROTOM_LINE, "|turn|1"])["effect_feats"]
    assert list(feats[GARCHOMP, EARTHQUAKE]) == [0, 0, 0, 0, 0, 0]


def test_prior_weights_types_and_statuses(enc):
    lines = [opponent_switch("Heatran"), "|turn|1"]
    obs = effects_of(enc, default_team(ROTOM), lines)
    rotom = obs["effect_feats"][0]  # poke-env's battle.team starts with the active Pokémon
    # Will-O-Wisp: a Fire type can't be burned (sure), and Flash Fire has weight 0.75
    assert rotom[WILL_O_WISP, NO_EFFECT] == 1
    assert rotom[WILL_O_WISP, ABILITY] == pytest.approx(0.75)
    assert not rotom[HYDRO_PUMP].any()
    # Heatran's candidate Earth Power into our Rotom-Wash's Levitate
    cand = obs["cand_effect_feats"][0]
    candidates = [m for m, _ in hand_priors().infer("heatran", [], k=8).candidates]
    earth_power = candidates.index("earthpower")
    assert list(cand[earth_power, :3]) == [1, 1, 0]
    assert not np.delete(cand, earth_power, axis=0)[:, :2].any()


def test_a_status_move_into_a_statused_target_fails(enc):
    lines = [opponent_switch("Venusaur"), "|-status|p2a: Venusaur|par", "|turn|1"]
    rotom = effects_of(enc, default_team(ROTOM), lines)["effect_feats"][0]
    assert list(rotom[WILL_O_WISP]) == [0, 0, 1, 0, 0, 0]


def test_failed_last_time_and_twice(enc):
    fail = ["|move|p1a: Garchomp|Stealth Rock|p2a: Venusaur", "|-fail|p1a: Garchomp"]
    lines = [opponent_switch("Venusaur"), "|turn|1", *fail, "|turn|2"]
    once = effects_of(enc, default_team(), lines)["effect_feats"][GARCHOMP, STEALTH_ROCK]
    twice = effects_of(enc, default_team(), lines + fail + ["|turn|3"])["effect_feats"]
    assert (once[FAILED], once[FAILED_TWICE]) == (1, 0)
    assert (twice[GARCHOMP, STEALTH_ROCK, FAILED], twice[GARCHOMP, STEALTH_ROCK, FAILED_TWICE]) == (1, 1)
    assert not twice[GARCHOMP, EARTHQUAKE].any()
    worked = lines + fail + ["|turn|3", "|move|p1a: Garchomp|Stealth Rock|p2a: Venusaur", "|turn|4"]
    assert not effects_of(enc, default_team(), worked)["effect_feats"][GARCHOMP, STEALTH_ROCK].any()


def test_without_the_flag_the_v2_layout_is_unchanged():
    plain = EncoderV2(hand_priors(), history=True)
    assert "effect_feats" not in plain.layout and "move_effects" not in plain.spec()
    with_effects = EncoderV2(hand_priors(), history=True, move_effects=True)
    assert list(with_effects.layout)[:-2] == list(plain.layout)
    assert list(with_effects.layout)[-2:] == ["effect_feats", "cand_effect_feats"]


@pytest.mark.parametrize("move_tokens", [False, True])
def test_with_move_effects_plays_like_the_model_it_extends(move_tokens):
    model = make_model("small", encoder_version=2, history=True, move_tokens=move_tokens).eval()
    extended = with_move_effects(model).eval()
    assert extended.cfg.move_effects and extended.priors is model.priors
    obs = {k: torch.from_numpy(np.stack([o[k] for o in model_obs(extended, 6)]))
           for k in model_obs(extended, 1)[0]}
    with torch.no_grad():
        before = model({k: v for k, v in obs.items() if not k.endswith("effect_feats")})
        after = extended(obs)
    torch.testing.assert_close(after[0], before[0])
    torch.testing.assert_close(after[1], before[1])
    # ...and its new inputs get a gradient
    extended.train()
    logits, value = extended(obs)
    (logits.masked_fill(~obs["mask"], 0).sum() + value.sum()).backward()
    grad = extended.move_net[0].weight.grad
    assert grad[:, -F_EFFECT:].abs().sum() > 0


class FakeBatcher:
    encoder = EncoderV1()

    async def act(self, obs, greedy=False):
        self.mask = obs["mask"]
        return int(np.flatnonzero(obs["mask"])[0]), 0.0, 0.0, None


@pytest.mark.parametrize("no_tera_before,tera", [(0, True), (1, True), (2, False)])
def test_no_tera_before_masks_the_tera_actions_until_that_turn(no_tera_before, tera):
    from players import RLPlayer
    battle = make_battle(default_team(), [opponent_switch("Venusaur"), "|turn|1"])
    batcher = FakeBatcher()
    player = RLPlayer(batcher, greedy=True, no_tera_before=no_tera_before, start_listening=False)
    asyncio.run(player._choose(battle))
    assert batcher.mask[10:].any() == tera
    assert batcher.mask[6:10].any()
