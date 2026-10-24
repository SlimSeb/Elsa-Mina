"""D2: the damage estimate and the matchup tokens."""

import math

import numpy as np
import pytest
import torch
from poke_env.battle import Move

import damage as D
from encoder_v2 import EncoderV2, F_MATCH
from tests.battles import default_team, feed, make_battle, mon, opponent_switch
from tests.priors import hand_priors


def test_random_battle_stats():
    # Garchomp (108 base HP / 130 Atk) at level 77
    assert D.random_battle_stat(108, 77, hp=True) == (2 * 108 + 52) * 77 // 100 + 77 + 10
    assert D.random_battle_stat(130, 77) == (2 * 130 + 52) * 77 // 100 + 5
    assert [D.boost_multiplier(s) for s in (-2, 0, 1, 6)] == [0.5, 1.0, 1.5, 4.0]


def test_damage_follows_the_documented_formula():
    battle = make_battle(default_team(), [opponent_switch("Venusaur"), "|turn|1"])
    garchomp, venusaur = battle.active_pokemon, battle.opponent_active_pokemon
    a, b = D.Fighter.of(garchomp, ours=True), D.Fighter.of(venusaur, ours=False)
    assert a.stats["atk"] == 200 and a.max_hp == 250              # the request's
    assert b.stats["def"] == D.random_battle_stat(83, 84) and b.hp == b.max_hp
    eq = Move("earthquake", gen=9)            # Ground on Grass/Poison: 0.5 x 2, neutral
    assert venusaur.damage_multiplier(eq) == 1.0
    base = math.floor(math.floor(math.floor(2 * 77 / 5 + 2) * 100 * 200 / b.stats["def"]) / 50) + 2
    assert D.damage(a, b, eq, battle, {}) == pytest.approx(base * 1.5 * D.MEAN_ROLL)  # STAB
    frac, ko = D.expected_fraction(a, b, eq, battle, {})
    assert frac == pytest.approx(min(base * 1.5 * D.MEAN_ROLL, b.hp) / b.max_hp)
    assert ko == (base * 1.5 * D.MEAN_ROLL >= b.hp)
    outrage = Move("outrage", gen=9)          # Dragon: STAB for Garchomp, not for Venusaur
    assert D.stab(garchomp, outrage) == 1.5 and D.stab(venusaur, outrage) == 1.0
    assert D.damage(a, b, Move("swordsdance", gen=9), battle, {}) == 0.0
    assert D.damage(a, b, Move("seismictoss", gen=9), battle, {}) == 77.0
    assert D.damage(a, b, Move("superfang", gen=9), battle, {}) == math.floor(b.hp / 2)
    # screens, burn, boosts
    from poke_env.battle import SideCondition
    with_reflect = D.damage(a, b, eq, battle, {SideCondition.REFLECT: 1})
    assert with_reflect == pytest.approx(D.damage(a, b, eq, battle, {}) / 2)
    feed(battle, "|-boost|p1a: Garchomp|atk|2")
    assert D.Fighter.of(garchomp, True).stat("atk") == 400


def test_speed_order_and_trick_room():
    battle = make_battle(default_team(), [opponent_switch("Venusaur"), "|turn|1"])
    a = D.Fighter.of(battle.active_pokemon, True)
    b = D.Fighter.of(battle.opponent_active_pokemon, False)
    assert a.stats["spe"] == 190 > b.stats["spe"]
    assert D.move_order(a, b, battle) == 1
    feed(battle, "|-fieldstart|move: Trick Room|[of] p2a: Venusaur")
    assert D.move_order(a, b, battle) == -1


def test_matchup_rows():
    enc = EncoderV2(hand_priors(), matchups=True)
    battle = make_battle(default_team(), [
        opponent_switch("Venusaur"), "|turn|1",
        "|move|p2a: Venusaur|Earth Power|p1a: Garchomp", "|turn|2"])
    feats = enc.encode(battle)["matchup_feats"]
    assert feats.shape == (12, F_MATCH)
    assert feats[:3, 0].tolist() == [1.0] * 3 and not feats[3:6].any()   # 3 of ours vs Venusaur
    assert feats[6, 0] == 1.0 and not feats[7:].any()                     # Garchomp vs Venusaur
    np.testing.assert_array_equal(feats[0, 1:], feats[6, 1:])             # same pair
    # Garchomp's 4 moves: Earthquake, Outrage hit; Stealth Rock, Swords Dance do not
    assert feats[0, 1] > 0 and feats[0, 2] > 0 and feats[0, 3] == feats[0, 4] == 0
    # Venusaur's one revealed move, Earth Power, on each of ours (abilities such as
    # Rotom-Wash's Levitate are not part of the estimate)
    assert feats[0, 5] > 0 and feats[0, 6:9].sum() == 0
    assert feats[1, 5] > feats[0, 5]          # 2x on Electric/Water, 1x on Dragon/Ground
    assert feats[0, 9] == feats[0, 1:5].max() and feats[0, 10] == feats[0, 5]
    assert feats[0, 13:16].sum() == 1.0


def test_matchup_tokens_join_after_half_the_layers():
    from tests.helpers import make_model
    from tests.test_model import batch_for
    model = make_model("small", encoder_version=2, matchup_tokens=True, typed_ffn=True).eval()
    assert model.inject_at == 1
    assert "matchups" not in model.layers[0].ffs and "matchups" in model.layers[1].ffs
    obs = batch_for(model)
    seen = []
    for layer in model.layers:
        layer.register_forward_hook(lambda mod, inp, out: seen.append(inp[0].shape[1]))
    with torch.no_grad():
        logits, value = model(obs)
    assert seen == [15, 27, 27]
    other = {k: v.clone() for k, v in obs.items()}
    other["matchup_feats"][..., 1:] = 1.0 - other["matchup_feats"][..., 1:]
    with torch.no_grad():
        assert not torch.allclose(model(other)[1], value)
    mask = model.attention_mask(torch.ones(2, 27, dtype=torch.bool), full=True)
    assert mask.shape == (2, 1, 27, 27) and mask[0, 0, 0, 26] and not mask[0, 0, 26, 0]
