"""encoder_v2: V1's arrays untouched, the priors arrays, and V1's action space."""

import numpy as np
import pytest
from poke_env.environment import SinglesEnv

import encoder as E
from encoder_v2 import EncoderV1, EncoderV2, F_CAND, F_PRIOR, TYPE_INDEX, encoder_for
from model import ModelConfig
from tests.battles import default_team, make_battle, opponent_switch
from tests.priors import hand_priors


@pytest.fixture(scope="module")
def enc():
    return EncoderV2(hand_priors(), cand_moves=8)


LINES = [opponent_switch("Venusaur"), "|turn|1",
         "|move|p2a: Venusaur|Earth Power|p1a: Garchomp", "|turn|2"]


def battles():
    yield "normal", make_battle(default_team(), LINES)
    yield "no tera", make_battle(default_team(1), LINES, can_tera=False)
    yield "forced switch", make_battle(default_team(), LINES, force_switch=True)
    yield "trapped", make_battle(default_team(2), LINES, trapped=True)
    yield "disabled", make_battle(default_team(), LINES, disabled=("outrage",))


@pytest.mark.parametrize("name,battle", list(battles()))
def test_v2_keeps_v1s_arrays_and_action_space(enc, name, battle):
    v1, v2 = E.encode(battle), enc.encode(battle)
    for k, v in v1.items():
        np.testing.assert_array_equal(v2[k], v, err_msg=k)
    assert list(v2) == enc.input_names and list(v2)[:6] == EncoderV1().input_names
    for k, (shape, dtype) in enc.layout.items():
        assert v2[k].shape == shape and v2[k].dtype == np.dtype(dtype), k


@pytest.mark.parametrize("name,battle", list(battles()))
def test_action_indices_map_to_the_same_poke_env_orders_as_v1(enc, name, battle):
    mask = enc.encode(battle)["mask"]
    np.testing.assert_array_equal(mask, E.action_mask(battle))
    team = list(battle.team.values())
    moves = list(battle.active_pokemon.moves) if battle.active_pokemon else []
    for a in np.flatnonzero(mask):
        order = E.action_to_order(int(a), battle).message
        assert order == SinglesEnv.action_to_order(np.int64(E.compact_to_env_action(int(a))),
                                                   battle, strict=False).message
        if a < 6:
            assert order == f"/choose switch {team[a].name}"
        else:
            assert order.startswith(f"/choose move {moves[(a - 6) % 4]}")
            assert order.endswith("terastallize") == (a >= 10)
        assert E.order_to_compact_action(E.action_to_order(int(a), battle), battle) == a


def test_priors_arrays(enc):
    battle = make_battle(default_team(), LINES)
    obs = enc.encode(battle)
    venusaur = 0  # first revealed opponent -> row 0
    p = hand_priors().infer("venusaur", ["earthpower"], k=8)
    assert [int(i) for i in obs["cand_ids"][venusaur][:len(p.candidates)]] == [
        E.MOVE_VOCAB[m] for m, _ in p.candidates]
    np.testing.assert_allclose(obs["cand_feats"][venusaur, :len(p.candidates), -1],
                               [f for _, f in p.candidates])
    assert obs["cand_feats"].shape[-1] == F_CAND == E.F_MOVE + 1
    # candidate features are V1 move features against our active, at full pp
    sludge = [m for m, _ in p.candidates].index("sludgebomb")
    from poke_env.battle import Move
    expected = E._move_features(Move("sludgebomb", gen=9), battle.opponent_active_pokemon,
                                battle.active_pokemon)
    np.testing.assert_allclose(obs["cand_feats"][venusaur, sludge, :E.F_MOVE], expected)
    assert expected[3] == 1.0  # pp fraction
    assert obs["prior_ids"][venusaur, 0, 0] == E.ABILITY_VOCAB["chlorophyll"]
    assert obs["prior_w"][venusaur, 0, :2] == pytest.approx([0.75, 0.25])
    assert not obs["prior_ids"][venusaur, 1].any()  # no items in the sets
    feats = obs["prior_feats"][venusaur]
    assert feats.shape == (F_PRIOR,)
    assert feats[TYPE_INDEX["steel"]] == pytest.approx(p.tera_types["steel"])
    assert list(feats[E.NT:]) == pytest.approx([1.0, 2 / 3, 0.0, 1.0])  # sets, 2 of 3 roles, -, choice


def test_unrevealed_and_unknown_opponents_stay_empty(enc):
    battle = make_battle(default_team(), [opponent_switch("Pikachu")])  # not in the sets
    obs = enc.encode(battle)
    for k in ("cand_ids", "cand_feats", "prior_ids", "prior_w", "prior_feats"):
        assert not obs[k].any(), k


def test_encoder_for_a_config():
    assert isinstance(encoder_for(ModelConfig()), EncoderV1)
    v2 = encoder_for(ModelConfig(encoder_version=2, cand_moves=5), hand_priors())
    assert isinstance(v2, EncoderV2) and v2.layout["cand_ids"][0] == (6, 5)
    with pytest.raises(ValueError):
        encoder_for(ModelConfig(encoder_version=2))
