"""Move pooling that ignores slot order (model.py move_pool) and the training-only opponent heads
(opp_targets.py's targets from re-simulations, model.py opp_heads, offline.py opp_coef and
opp_metrics, human_data.py's dataset fields)."""

import copy
import dataclasses
import json
import math
import zlib
from collections import Counter
from types import SimpleNamespace

import numpy as np
import onnx
import pytest
import torch

import encoder as E
import human_data as H
import offline as O
import opp_targets as T
import resim as R
from dataset import ShardDataset, ShardWriter, discounted_returns
from encoder_v2 import V1_LAYOUT
from export_onnx import export, model_inputs, parity, random_inputs
from model import (OPP_HEADS, PRESETS, MiniPokeTransformer, ModelConfig, build_model, count_params,
                   with_opp_heads)
from tests.helpers import make_model, random_obs
from tests.test_resim import STEMS, resim_of, stored

PERM = [2, 0, 3, 1]
RELEASED_OBS = dict(encoder_version=2, history=True, move_effects=True)   # V2.1, MiniPokeV3_22M's


def batch_for(model, n=6, seed=0):
    return dict(zip(model_inputs(model), random_inputs(n, seed, model=model)))


def other_side(side: str) -> str:
    return R.SIDES[1 - R.SIDES.index(side)]


# --------------------------------------------------------------------------
# Move pooling
# --------------------------------------------------------------------------
def permute_moves(obs, perm):
    """The same observations with every Pokémon's move slots in another order: new slot j holds
    old slot perm[j] (ids, features, effects, and the move actions of the mask)."""
    p = torch.tensor(perm)
    out = {k: v.clone() for k, v in obs.items()}
    for key in ("move_ids", "move_feats", "effect_feats"):
        if key in out:
            out[key] = obs[key][:, :, p]
    out["mask"][:, 6:10] = obs["mask"][:, 6 + p]
    out["mask"][:, 10:14] = obs["mask"][:, 10 + p]
    return out


@pytest.mark.parametrize("cfg", [dict(move_pool="meanmax"), dict(move_pool="meanmax", **RELEASED_OBS)],
                         ids=["v1", "v2-history-effects"])
def test_meanmax_ignores_the_order_of_the_move_slots(cfg):
    model = make_model("small", **cfg).eval()
    obs = batch_for(model)
    with torch.no_grad():
        logits, value = model(obs)
        moved, moved_value = model(permute_moves(obs, PERM))
    p = torch.tensor(PERM)
    torch.testing.assert_close(moved_value, value)
    torch.testing.assert_close(moved[:, :6], logits[:, :6])                 # switches
    torch.testing.assert_close(moved[:, 6:10], logits[:, 6 + p])            # moves follow their slot
    torch.testing.assert_close(moved[:, 10:14], logits[:, 10 + p])


def test_concat_depends_on_the_order_of_the_move_slots():
    model = make_model("small").eval()
    obs = batch_for(model)
    with torch.no_grad():
        assert not torch.allclose(model(permute_moves(obs, PERM))[1], model(obs)[1])


def test_meanmax_pools_the_moves_held_and_a_pokemon_without_moves_to_zero():
    model = make_model("small", move_pool="meanmax")
    d = model.cfg.d_model
    present = torch.ones(2, E.N_MONS, E.N_MOVES, 1)
    present[0, 3] = 0.0                  # no move known
    present[1, 5, 1:] = 0.0              # one move known
    move_vec = torch.randn(2, E.N_MONS, E.N_MOVES, d) * present
    pooled = model._pool_moves(move_vec, present)
    assert pooled.shape == (2, E.N_MONS, 2 * d)
    assert (pooled[0, 3] == 0).all()
    torch.testing.assert_close(pooled[1, 5], torch.cat([move_vec[1, 5, 0], move_vec[1, 5, 0]]))
    torch.testing.assert_close(pooled[0, 0], torch.cat([move_vec[0, 0].mean(0), move_vec[0, 0].amax(0)]))


def test_an_unknown_move_pool_is_refused():
    with pytest.raises(ValueError, match="move_pool"):
        MiniPokeTransformer(ModelConfig(move_pool="sum"))


def test_meanmax_has_about_a_million_fewer_parameters_on_large_v2():
    concat = count_params(MiniPokeTransformer(dataclasses.replace(PRESETS["large"], encoder_version=2)))
    meanmax = count_params(MiniPokeTransformer(dataclasses.replace(PRESETS["large"], encoder_version=2,
                                                                   move_pool="meanmax")))
    assert 0.9e6 < concat - meanmax < 1.2e6


def test_move_tokens_read_no_pooled_moves():
    concat = make_model("small", encoder_version=2, move_tokens=True).eval()
    meanmax = make_model("small", encoder_version=2, move_tokens=True, move_pool="meanmax").eval()
    assert count_params(concat) == count_params(meanmax)
    obs = batch_for(concat)
    with torch.no_grad():
        for a, b in zip(concat(obs), meanmax(obs)):
            torch.testing.assert_close(a, b)


def signature(graph):
    return [(v.name, v.type.tensor_type.elem_type, [d.dim_param or d.dim_value for d in v.type.tensor_type.shape.dim])
            for v in list(graph.input) + list(graph.output)]


def initializer_size(graph) -> int:
    return sum(int(np.prod(init.dims)) for init in graph.initializer)


def test_meanmax_exports_the_same_inputs_outputs_and_spec(tmp_path):
    concat = make_model("small", **RELEASED_OBS)
    meanmax = make_model("small", move_pool="meanmax", **RELEASED_OBS)
    export(concat, tmp_path / "concat")
    path = export(meanmax, tmp_path / "meanmax")
    diff = parity(meanmax, path, batch=9)
    assert diff["logits"] < 1e-4 and diff["value"] < 1e-5
    assert signature(onnx.load(str(path)).graph) == signature(onnx.load(str(tmp_path / "concat/minipoke.onnx")).graph)
    assert (tmp_path / "meanmax/encoder_spec.json").read_bytes() == (tmp_path / "concat/encoder_spec.json").read_bytes()


# --------------------------------------------------------------------------
# The opponent heads
# --------------------------------------------------------------------------
HEADS = {"v1": dict(), "v2": dict(encoder_version=2), "v2.1-meanmax": dict(move_pool="meanmax", **RELEASED_OBS),
         "v1-move-tokens": dict(move_tokens=True), "v2-move-tokens": dict(encoder_version=2, move_tokens=True)}


@pytest.mark.parametrize("name", list(HEADS))
def test_opponent_heads_shapes_and_masks(name):
    model = make_model("small", opp_heads=True, **HEADS[name]).eval()
    k = model.cfg.cand_moves if model.cfg.encoder_version == 2 else 0
    obs = batch_for(model, n=5)
    feats = obs["mon_feats"]
    feats[:, E.N_SLOTS:, 0] = 1.0          # every opposing slot revealed...
    feats[1, 9:, 0] = 0.0                  # ...but 3 in row 1
    feats[:, E.N_SLOTS:, 3] = 0.0
    for b in range(4):                     # an opposing active in rows 0-3, none in row 4
        feats[b, E.N_SLOTS + b, 3] = 1.0
    obs["move_feats"][2, E.N_SLOTS + 2, 2, 0] = 0.0     # row 2's active has no 3rd move
    with torch.no_grad():
        out = model.forward_train(obs, opp=True)[3]
    n = T.n_opp_classes(k)
    assert out["opp_act"].shape == out["opp_act_mask"].shape == (5, n) and out["opp_tera"].shape == (5,)
    assert out["opp_item"].shape == (5, E.N_SLOTS, E.VOCAB_SIZES["item"])
    assert out["opp_ability"].shape == (5, E.N_SLOTS, E.VOCAB_SIZES["ability"])
    assert out["opp_tera_type"].shape == (5, E.N_SLOTS, E.NT)
    assert out["opp_moves"].shape == (5, E.N_SLOTS, E.VOCAB_SIZES["move"])
    mask = out["opp_act_mask"]
    assert mask[0, :T.OPP_UNREVEALED].all() and not mask[0, T.OPP_UNREVEALED]
    assert mask[1, :3].all() and not mask[1, 3:6].any() and mask[1, T.OPP_UNREVEALED]
    for b in range(4):
        known = obs["move_feats"][b, E.N_SLOTS + b, :, 0] > 0
        assert mask[b, T.OPP_MOVE0:T.OPP_CAND0].tolist() == known.tolist()
        if k:
            assert mask[b, T.OPP_CAND0:T.opp_other(k)].tolist() == (obs["cand_feats"][b, b, :, 0] > 0).tolist()
    assert not mask[2, T.OPP_MOVE0 + 2]
    assert mask[:4, T.opp_other(k)].all() and not mask[4, T.OPP_MOVE0:].any()
    assert mask[4, :T.OPP_UNREVEALED].all()          # a switch needs no active
    logits = out["opp_act"]
    assert (logits[~mask] <= -1e8).all() and torch.isfinite(logits[mask]).all() and (logits[mask] > -1e8).all()


def test_forward_train_refuses_opp_without_the_heads():
    model = make_model("small")
    with pytest.raises(ValueError, match="no opponent heads"):
        model.forward_train(batch_for(model), opp=True)


@pytest.mark.parametrize("cfg", [dict(), RELEASED_OBS], ids=["v1", "v2.1"])
def test_opponent_heads_change_neither_forward_nor_the_export(cfg, tmp_path):
    plain = make_model("small", **cfg).eval()
    heads = make_model("small", opp_heads=True, **cfg).eval()      # same seed: the same other weights
    added = with_opp_heads(copy.deepcopy(plain)).eval()
    obs = batch_for(plain)
    with torch.no_grad():
        expected = plain(obs)
        for model in (heads, added):
            for a, b in zip(model(obs), expected):
                assert torch.equal(a, b)
    graphs = {}
    for name, model in (("plain", plain), ("heads", heads), ("added", added)):
        export(model, tmp_path / name)
        graphs[name] = onnx.load(str(tmp_path / name / "minipoke.onnx")).graph
    for name in ("heads", "added"):
        assert signature(graphs[name]) == signature(graphs["plain"])
        assert initializer_size(graphs[name]) == initializer_size(graphs["plain"])
        assert (tmp_path / name / "encoder_spec.json").read_bytes() == (tmp_path / "plain/encoder_spec.json").read_bytes()


def test_with_opp_heads_adds_fresh_heads_and_keeps_the_rest():
    model = make_model("small", encoder_version=2)
    before = {k: v.clone() for k, v in model.state_dict().items()}
    assert with_opp_heads(model) is model and model.cfg.opp_heads
    state = model.state_dict()
    assert all(torch.equal(state[k], v) for k, v in before.items())
    assert {k.split(".")[0] for k in state if k not in before} == set(OPP_HEADS)
    n = count_params(model)
    with_opp_heads(model)
    assert count_params(model) == n
    model.forward_train(batch_for(model), opp=True)


def test_a_checkpoint_with_heads_reloads_with_them(tmp_path):
    from checkpoint import load_checkpoint, save_checkpoint
    model = make_model("small", opp_heads=True, move_pool="meanmax", **RELEASED_OBS).eval()
    save_checkpoint(tmp_path / "m.pt", model)
    loaded = build_model(load_checkpoint(tmp_path / "m.pt")).eval()
    assert loaded.cfg == model.cfg and loaded.cfg.opp_heads and loaded.cfg.move_pool == "meanmax"
    obs = batch_for(model)
    with torch.no_grad():
        ours, theirs = model.forward_train(obs, opp=True)[3], loaded.forward_train(obs, opp=True)[3]
    assert all(torch.equal(ours[k], theirs[k]) for k in ours)


def test_checkpoints_from_before_the_new_fields_load_with_their_defaults():
    model = MiniPokeTransformer(PRESETS["small"])
    cfg = {k: v for k, v in model.cfg.to_dict().items() if k not in ("move_pool", "opp_heads")}
    loaded = build_model({"model": model.state_dict(), "cfg": {**cfg, "aux_heads": True}})
    assert loaded.cfg.move_pool == "concat" and not loaded.cfg.opp_heads


@pytest.mark.skipif(not torch.backends.mps.is_available(), reason="no MPS")
def test_opponent_heads_and_meanmax_train_on_mps_without_cpu_fallback():
    import runtime
    runtime.install_mps_fallback_warning()
    before = len(runtime.mps_fallback_ops)
    model = make_model("small", opp_heads=True, move_pool="meanmax", **RELEASED_OBS).to("mps").train()
    obs = {k: v.to("mps") for k, v in batch_for(model, n=8).items()}
    out = model.forward_train(obs, opp=True)[3]
    targets = {k: torch.from_numpy(v).to("mps") for k, v in synthetic_targets(np.random.default_rng(0), 8).items()}
    loss, _ = O.opp_loss(out, targets)
    loss.backward()
    torch.mps.synchronize()
    assert torch.isfinite(loss) and len(runtime.mps_fallback_ops) == before, runtime.mps_fallback_ops


# --------------------------------------------------------------------------
# The loss
# --------------------------------------------------------------------------
def uniform_outputs(batch: int, k: int):
    n = T.n_opp_classes(k)
    return {"opp_act": torch.zeros(batch, n), "opp_act_mask": torch.ones(batch, n, dtype=torch.bool),
            "opp_tera": torch.zeros(batch),
            "opp_item": torch.zeros(batch, E.N_SLOTS, E.VOCAB_SIZES["item"]),
            "opp_ability": torch.zeros(batch, E.N_SLOTS, E.VOCAB_SIZES["ability"]),
            "opp_tera_type": torch.zeros(batch, E.N_SLOTS, E.NT),
            "opp_moves": torch.zeros(batch, E.N_SLOTS, E.VOCAB_SIZES["move"])}


def synthetic_targets(rng, n: int, k: int = 0):
    """Learnable opponent targets (a few values each) for n rows, with some -1."""
    act = rng.choice([T.OPP_SWITCH0 + 1, T.OPP_UNREVEALED, T.opp_other(k)], n).astype(np.int16)
    act[::7] = T.NO_TARGET
    tera = rng.choice([0, 0, 0, 1], n).astype(np.int8)
    tera[::5] = T.NO_TARGET
    item = rng.choice([40, 41], (n, E.N_SLOTS)).astype(np.int16)
    item[:, 4:] = T.NO_TARGET
    moves = rng.choice([20, 21, 22, 23, 24], (n, E.N_SLOTS, E.N_MOVES)).astype(np.int16)
    moves[:, :, 3] = E.PAD_ID
    moves[:, 4:] = T.NO_TARGET
    return {"opp_act": act, "opp_tera": tera, "opp_item": item,
            "opp_ability": rng.choice([10, 11], (n, E.N_SLOTS)).astype(np.int16),
            "opp_tera_type": rng.choice([3, 4], (n, E.N_SLOTS)).astype(np.int8), "opp_moves": moves}


def test_the_opponent_loss_is_one_for_uniform_heads_and_skips_what_has_no_target():
    targets = {k: torch.from_numpy(v).long() for k, v in synthetic_targets(np.random.default_rng(1), 14, 8).items()}
    out = uniform_outputs(14, 8)
    total, terms = O.opp_loss(out, targets)
    assert total.item() == pytest.approx(1.0, abs=1e-5)
    assert all(t.item() == pytest.approx(1.0, abs=1e-5) for t in terms.values())
    # rows without a target, or with one our view rules out, don't count
    out["opp_act"][0, 0] = -1e9                         # row 0: no target (-1 reads class 0)
    targets["opp_act"][1] = T.OPP_UNREVEALED
    out["opp_act_mask"][1, T.OPP_UNREVEALED] = False    # row 1: a class the view masks
    out["opp_act"][1, T.OPP_UNREVEALED] = -1e9
    assert O.opp_loss(out, targets)[1]["act"].item() == pytest.approx(1.0, abs=1e-5)
    # no choice target at all: the mean of the other five
    targets["opp_act"][:] = T.NO_TARGET
    total, terms = O.opp_loss(out, targets)
    assert terms["act"].item() == 0.0 and total.item() == pytest.approx(1.0, abs=1e-5)


# --------------------------------------------------------------------------
# Targets on the stored re-simulations (tests/fixtures/resim)
# --------------------------------------------------------------------------
@pytest.fixture(scope="module")
def v3():
    return R.parity_encoders(["MiniPokeV3_22M"])["MiniPokeV3_22M"]


@pytest.fixture(scope="module")
def fixture_targets(v3):
    """(stem, side) -> (res, decisions, targets, paired)."""
    out = {}
    for stem in STEMS:
        res = resim_of(stem, stored(stem))
        for side in R.SIDES:
            decisions = R.side_decisions(res.sides[side], res.replay.name(side), {"s": v3}, stem)["decisions"]
            out[stem, side] = (res, decisions, *T.side_targets(res, side, decisions))
    return out


def opponent_records(res, side, decisions):
    """The opponent's request record paired with each decision."""
    index = {r["line"]: k for k, r in enumerate(res.requests[side])}
    return [res.requests[other_side(side)][index[d["line"]]] for d in decisions]


def test_the_two_sides_requests_pair_up_in_every_fixture():
    for stem in STEMS:
        out = stored(stem)
        p1, p2 = out["requests"]["p1"], out["requests"]["p2"]
        assert len(p1) == len(p2)
        rqids = {side: [T.parse_request(out["sides"][side][r["line"]])["rqid"] for r in out["requests"][side]]
                 for side in R.SIDES}
        assert [b - a for a, b in zip(rqids["p1"], rqids["p2"])] == [1] * len(p1)   # one batch each
        assert not any(a["wait"] and b["wait"] for a, b in zip(p1, p2))


def test_the_opponents_wait_records_have_no_choice_target(fixture_targets):
    waits = 0
    for (stem, side), (res, decisions, targets, paired) in fixture_targets.items():
        assert paired
        for record, target in zip(opponent_records(res, side, decisions), targets):
            if record["wait"]:
                assert target["opp_act"] == T.NO_TARGET and target["opp_tera"] == T.NO_TARGET
                waits += 1
    assert waits > 0


def test_choice_targets_are_the_opponents_real_choice(fixture_targets):
    seen = Counter()
    for (stem, side), (res, decisions, targets, _) in fixture_targets.items():
        opp = other_side(side)
        for d, record, target in zip(decisions, opponent_records(res, side, decisions), targets):
            if record["wait"]:
                continue
            choice = R.as_choice(opp, record["choice"])
            request = T.parse_request(res.sides[opp][record["line"]])
            obs, act, team = d["obs"]["s"], target["opp_act"], d["opponent_team"]
            if choice.kind == "switch":
                ident = request["side"]["pokemon"][choice.position - 1]["ident"]
                assert act == (team.index(ident) if ident in team else T.OPP_UNREVEALED)
                seen["switch" if ident in team else "unrevealed"] += 1
                assert target["opp_tera"] == T.NO_TARGET
                continue
            assert choice.kind == "move"
            move = choice.move
            if move.isdigit():
                move = request["active"][0]["moves"][int(move) - 1]["id"]
            move_id = E.MOVE_VOCAB[move]
            a = int(obs["mon_feats"][E.N_SLOTS:, 3].argmax())
            known, cands = obs["move_ids"][E.N_SLOTS + a], obs["cand_ids"][a]
            k = len(cands)
            if T.OPP_MOVE0 <= act < T.OPP_CAND0:
                assert known[act - T.OPP_MOVE0] == move_id
                seen["known"] += 1
            elif T.OPP_CAND0 <= act < T.opp_other(k):
                assert cands[act - T.OPP_CAND0] == move_id and move_id not in known
                seen["candidate"] += 1
            else:
                assert act == T.opp_other(k) and move_id not in known and move_id not in cands
                seen["other"] += 1
            can_tera = bool(request["active"][0].get("canTerastallize"))
            assert target["opp_tera"] == (int(choice.tera) if can_tera else T.NO_TARGET)
            seen["tera"] += target["opp_tera"] == 1
    # every switch these opponents made while we chose went to a Pokémon not revealed yet
    # (test_choice_target_classes covers the others)
    assert seen["unrevealed"] and seen["known"] and seen["candidate"] and seen["tera"], seen


def test_choice_target_classes():
    from analyze_replays import parse_choice
    obs = {"mon_feats": np.zeros((E.N_MONS, E.F_MON), dtype=np.float32),
           "move_ids": np.zeros((E.N_MONS, E.N_MOVES), dtype=np.int64),
           "cand_ids": np.zeros((E.N_SLOTS, 8), dtype=np.int64)}
    obs["mon_feats"][E.N_SLOTS + 1, 3] = 1.0          # their active: our view's opposing slot 1
    obs["move_ids"][E.N_SLOTS + 1, :2] = [E.MOVE_VOCAB["earthquake"], E.MOVE_VOCAB["stoneedge"]]
    obs["cand_ids"][1, :2] = [E.MOVE_VOCAB["swordsdance"], E.MOVE_VOCAB["uturn"]]
    team = ["p2: Garchomp", "p2: Landorus"]
    request = {"active": [{"moves": [{"move": "Stealth Rock", "id": "stealthrock"},
                                     {"move": "Earthquake", "id": "earthquake"}], "canTerastallize": "Ground"}],
               "side": {"pokemon": [{"ident": "p2: Landorus"}, {"ident": "p2: Garchomp"},
                                    {"ident": "p2: Kingambit"}]}}

    def target(text, req=request, view=obs):
        return T.choice_target(view, team, parse_choice(f">p2 {text}"), req)
    assert target("switch 2") == (T.OPP_SWITCH0, -1)                # Garchomp: our view's slot 0
    assert target("switch 3") == (T.OPP_UNREVEALED, -1)
    assert target("switch 7") == (-1, -1)
    assert target("move stoneedge terastallize") == (T.OPP_MOVE0 + 1, 1)
    assert target("move 2") == (T.OPP_MOVE0, 0)                     # the request's 2nd: earthquake
    assert target("move uturn") == (T.OPP_CAND0 + 1, 0)
    assert target("move 1") == (T.opp_other(8), 0)                  # stealthrock: neither known nor candidate
    assert target("move 3") == (-1, -1)
    assert target("move recharge") == (-1, -1)
    assert target("pass") == (-1, -1)
    assert T.choice_target(obs, team, None, request) == (-1, -1)
    no_tera = {**request, "active": [{"moves": request["active"][0]["moves"]}]}
    assert target("move earthquake terastallize", req=no_tera) == (T.OPP_MOVE0, -1)
    v1 = {k: v for k, v in obs.items() if k != "cand_ids"}
    assert target("move uturn", view=v1) == (T.opp_other(0), 0)
    no_active = {**obs, "mon_feats": np.zeros_like(obs["mon_feats"])}
    assert target("move earthquake", view=no_active) == (-1, -1)
    assert target("switch 2", view=no_active) == (T.OPP_SWITCH0, -1)   # a switch needs no active


def test_set_targets_of_a_request():
    request = {"side": {"pokemon": [{"ident": "p2: Garchomp", "item": "", "baseAbility": "roughskin",
                                     "teraType": "Stellar", "moves": ["earthquake", "notamove"]}]}}
    targets = T.set_targets(request, ["p2: Garchomp", "p2: Zoroark"])
    assert targets["opp_item"].tolist() == [E.PAD_ID] + [-1] * 5
    assert targets["opp_ability"][0] == E.ABILITY_VOCAB["roughskin"] and targets["opp_ability"][1] == -1
    assert targets["opp_tera_type"][0] == T.TERA_TYPES.index("stellar") and E.TYPES[-1].name == "STELLAR"
    assert targets["opp_moves"][0].tolist() == [E.MOVE_VOCAB["earthquake"], E.UNK_ID, E.PAD_ID, E.PAD_ID]
    assert (targets["opp_moves"][1:] == -1).all()
    assert all((v == -1).all() for v in T.set_targets(None, ["p2: Garchomp"]).values())
    assert T.item_index("leftovers") == 2 + zlib.crc32(b"leftovers") % E.N_ITEM_BUCKETS


def test_set_targets_are_the_opponents_first_request(fixture_targets):
    revealed_items = Counter()
    for (stem, side), (res, decisions, targets, _) in fixture_targets.items():
        opp = other_side(side)
        first = next(T.parse_request(res.sides[opp][r["line"]]) for r in res.requests[opp] if not r["wait"])
        by_ident = {p["ident"]: p for p in first["side"]["pokemon"]}
        for d, target in zip(decisions, targets):
            team = d["opponent_team"]
            assert set(team) <= set(by_ident)          # our view's keys are the request's idents
            for j in range(E.N_SLOTS):
                fields = [target[k][j] for k in ("opp_item", "opp_ability", "opp_tera_type")]
                if j >= len(team):
                    assert fields == [T.NO_TARGET] * 3 and (target["opp_moves"][j] == T.NO_TARGET).all()
                    continue
                mon = by_ident[team[j]]
                item = E.PAD_ID if not mon["item"] else 2 + zlib.crc32(mon["item"].encode()) % E.N_ITEM_BUCKETS
                assert fields == [item, E.ABILITY_VOCAB[mon["baseAbility"]],
                                  T.TERA_TYPES.index(mon["teraType"].lower())]
                assert [m for m in target["opp_moves"][j] if m] == [E.MOVE_VOCAB[m] for m in mon["moves"]]
                seen_item = int(d["obs"]["s"]["mon_ids"][E.N_SLOTS + j, 1])
                if seen_item > E.UNK_ID:               # an item our view knows: the encoder's own id
                    revealed_items[seen_item == item] += 1
    assert revealed_items[True] > 3 * revealed_items[False]   # Knock Off, Trick... change some


def test_unequal_request_counts_give_no_choice_targets_but_keep_the_sets(fixture_targets):
    res, decisions, targets, _ = fixture_targets[STEMS[0], "p1"]
    broken = dataclasses.replace(res, requests={**res.requests, "p2": res.requests["p2"][:-1]})
    unpaired, paired = T.side_targets(broken, "p1", decisions)
    assert not paired
    assert all(t["opp_act"] == T.NO_TARGET and t["opp_tera"] == T.NO_TARGET for t in unpaired)
    for a, b in zip(targets, unpaired):
        for key in ("opp_item", "opp_ability", "opp_tera_type", "opp_moves"):
            assert np.array_equal(a[key], b[key])
    assert any((t["opp_item"] >= 0).any() for t in unpaired)


def test_encode_side_rows_read_back_from_the_dataset_with_their_targets(v3, tmp_path):
    stem = STEMS[1]
    res = resim_of(stem, stored(stem))
    rows, stats = H.encode_side(res, "p2", v3, 1.0, 0.1, 0.995)
    targets = [row[-1] for row in rows]
    assert stats["opp_act_targets"] == sum(t["opp_act"] >= 0 for t in targets) > 0
    assert f"with the opponent's choice {stats['opp_act_targets']}" in H.describe(
        {"stats": dict(stats), "samples": {}, "dates": {}})
    writer = ShardWriter(tmp_path / "d", shard_size=8, obs_layout=v3.layout, extra=H.EXTRA)
    for step, (obs, action, ok, reward, ret, done, turn, opp) in enumerate(rows):
        writer.add(obs, np.zeros(E.ACT_DIM, dtype=np.float32), action, 0.0, ret, traj=0, step=step,
                   reward=reward, done=int(done), rating=1600, turn=turn, label_ok=int(ok), result=1, **opp)
    writer.close()
    data = ShardDataset(tmp_path / "d", val_frac=0.0)
    assert O.has_opp_targets(data)
    assert data.column("opp_act").tolist() == [t["opp_act"] for t in targets]
    batches = list(data.batches("train", 5))
    for key in T.OPP_EXTRA:
        assert np.array_equal(np.concatenate([b[key] for b in batches]), np.stack([t[key] for t in targets])), key


# --------------------------------------------------------------------------
# Offline training with the heads (synthetic V1 data)
# --------------------------------------------------------------------------
OLD_EXTRA = {k: v for k, v in H.EXTRA.items() if k not in T.OPP_EXTRA}


def write_opp_split(root, n_traj: int, seed: int, first_traj: int = 0, targets: bool = True) -> None:
    rng = np.random.default_rng(seed)
    obs = random_obs(n_traj * 10, seed)
    opp = synthetic_targets(rng, n_traj * 10)
    writer = ShardWriter(root, shard_size=64, obs_layout=V1_LAYOUT, extra=H.EXTRA if targets else OLD_EXTRA)
    for t in range(n_traj):
        rewards = [0.0] * 9 + [float(rng.choice([1.0, -1.0]))]
        rets = discounted_returns(rewards, 0.995)
        for s in range(10):
            row = t * 10 + s
            o = obs[row]
            o["mon_feats"][E.N_SLOTS:, 0] = 1.0
            o["mon_feats"][E.N_SLOTS:, 3] = 0.0
            o["mon_feats"][E.N_SLOTS + int(rng.integers(E.N_SLOTS)), 3] = 1.0
            o["mon_feats"][-1, 0] = 0.0                # a slot not revealed yet
            o["mon_ids"][E.N_SLOTS:, 1:] = E.UNK_ID     # items and abilities not seen yet
            action = int(rng.choice(np.flatnonzero(o["mask"])))
            probs = np.zeros(E.ACT_DIM, dtype=np.float32)
            probs[action] = 1.0
            extra = {k: v[row] for k, v in opp.items()} if targets else {}
            writer.add(o, probs, action, 0.0, float(rets[s]), traj=first_traj + t, step=s,
                       reward=rewards[s], done=int(s == 9), rating=1600, turn=s + 1, label_ok=1,
                       result=int(rewards[-1]), **extra)
    writer.close()


def make_dataset(root, targets: bool):
    for i, (split, n) in enumerate((("train", 24), ("val", 6), ("test", 6))):
        write_opp_split(root / split, n, seed=i, first_traj=1000 * i, targets=targets)
    (root / "meta.json").write_text(json.dumps({"model_flags": {"encoder_version": 1},
                                                "set_priors": None, "gamma": 0.995}))
    return root


@pytest.fixture
def opp_data(tmp_path):
    return make_dataset(tmp_path / "opp", targets=True)


@pytest.fixture
def old_data(tmp_path):
    return make_dataset(tmp_path / "old", targets=False)


def train_offline(root, out, model, **cfg):
    from checkpoint import save_checkpoint
    torch.manual_seed(0)
    config = O.OfflineConfig(minibatch=64, warmup_steps=2, patience=5, **cfg)
    out.mkdir(parents=True, exist_ok=True)
    return O.offline_train(model, O.HumanData(root), config, "cpu", out, save_checkpoint, log=lambda *_: None)


def test_offline_training_with_opp_coef_lowers_the_opponent_loss(opp_data, tmp_path):
    result = train_offline(opp_data, tmp_path / "run", make_model("small", opp_heads=True),
                           epochs=3, lr=3e-3, opp_coef=0.5)
    opp = [h["opp"] for h in result["history"]]
    assert opp[-1] < 0.8 * opp[0] and opp[-1] < 0.7, opp      # ~1 at init
    assert all(0 <= h["val_opp_top1"] <= 1 for h in result["history"])
    from checkpoint import load_checkpoint
    best = build_model(load_checkpoint(tmp_path / "run" / "offline.pt"))
    m = O.opp_metrics(best, O.HumanData(opp_data)["test"], "cpu")
    assert m["choice"]["n"] > 0 and 0 <= m["choice"]["top1"] <= 1 and m["choice"]["nll"] > 0
    assert m["item"]["top1"] > 0.3 and m["moves"]["recall4"] > 0.5   # learned the few values used
    assert "recall@4" in O.describe_opp_metrics(m)


def test_offline_training_refuses_opp_coef_without_heads_or_targets(opp_data, old_data, tmp_path):
    with pytest.raises(ValueError, match="this model has none"):
        train_offline(opp_data, tmp_path / "a", make_model("small"), epochs=1, opp_coef=0.5)
    with pytest.raises(ValueError, match="needs the opponent targets"):
        train_offline(old_data, tmp_path / "b", make_model("small", opp_heads=True), epochs=1, opp_coef=0.5)


def test_a_model_with_heads_trains_with_opp_coef_0_on_an_old_dataset(old_data, tmp_path):
    model = make_model("small", opp_heads=True)
    heads = {k: v.clone() for k, v in model.state_dict().items() if k.startswith("opp_")}
    result = train_offline(old_data, tmp_path / "run", model, epochs=1, opp_coef=0.0)
    assert math.isnan(result["history"][0]["opp"]) and "val_opp_top1" not in result["history"][0]
    state = model.state_dict()
    assert all(torch.equal(state[k], v) for k, v in heads.items())       # never trained


def test_train_offline_model_options(opp_data, tmp_path):
    import train
    from checkpoint import save_checkpoint
    args = train.build_parser().parse_args(["offline", "--data", str(opp_data), "--size", "small",
                                            "--move-pool", "meanmax", "--opp-heads"])
    assert args.opp_coef == 0.5
    data = O.HumanData(opp_data)
    model = train.offline_model(args, data)
    assert model.cfg.move_pool == "meanmax" and model.cfg.opp_heads
    save_checkpoint(tmp_path / "v3.pt", make_model("small"))
    init = SimpleNamespace(**{**vars(args), "init": str(tmp_path / "v3.pt"), "move_pool": None})
    model = train.offline_model(init, data)
    assert model.cfg.move_pool == "concat" and model.cfg.opp_heads      # heads added to --init's model
    train.offline_model(SimpleNamespace(**{**vars(init), "move_pool": "concat"}), data)   # the same: fine
    with pytest.raises(SystemExit, match="--move-pool"):
        train.offline_model(SimpleNamespace(**{**vars(init), "move_pool": "meanmax"}), data)
