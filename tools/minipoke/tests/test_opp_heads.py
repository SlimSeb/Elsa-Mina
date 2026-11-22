"""Order-free move pooling (ModelConfig.move_pool) and the opponent heads (ModelConfig.opp_heads,
opp_targets.py, offline.py's opp_coef). CPU; the re-simulation tests read the stored fixtures."""

import copy
import json

import numpy as np
import pytest
import torch

import encoder as E
import human_data as H
import offline as O
import opp_targets as T
from dataset import ShardWriter, discounted_returns
from encoder_v2 import V1_LAYOUT
from export_onnx import model_inputs, random_inputs
from model import MiniPokeTransformer, PRESETS, build_model, preset, with_opp_heads
from tests.helpers import make_model, random_obs


def batch_for(model, n=6, seed=0):
    return dict(zip(model_inputs(model), random_inputs(n, seed, model=model)))


def permute_moves(obs, perm):
    """Every Pokémon's 4 move slots reordered by `perm`, and our action mask with them."""
    out = {k: v.clone() for k, v in obs.items()}
    for k in ("move_ids", "move_feats", "effect_feats"):
        if k in out:
            out[k] = out[k][:, :, perm]
    mask = out["mask"].clone()
    mask[:, 6:10] = obs["mask"][:, 6:10][:, perm]
    mask[:, 10:14] = obs["mask"][:, 10:14][:, perm]
    out["mask"] = mask
    return out


# --------------------------------------------------------------------------
# Move pooling
# --------------------------------------------------------------------------
@pytest.mark.parametrize("cfg", [dict(), dict(encoder_version=2, history=True, move_effects=True)])
def test_meanmax_pooling_makes_the_model_blind_to_move_order(cfg):
    perm = [2, 0, 3, 1]
    model = make_model("small", move_pool="meanmax", **cfg).eval()
    obs = batch_for(model)
    with torch.no_grad():
        logits, value = model(obs)
        logits_p, value_p = model(permute_moves(obs, perm))
    assert torch.allclose(value, value_p, atol=1e-5)
    assert torch.allclose(logits[:, :6], logits_p[:, :6], atol=1e-5)          # switches
    assert torch.allclose(logits[:, 6:10][:, perm], logits_p[:, 6:10], atol=1e-5)
    assert torch.allclose(logits[:, 10:14][:, perm], logits_p[:, 10:14], atol=1e-5)


def test_concat_pooling_still_reads_the_move_order():
    model = make_model("small").eval()
    obs = batch_for(model)
    with torch.no_grad():
        assert not torch.allclose(model(obs)[1], model(permute_moves(obs, [2, 0, 3, 1]))[1])


def test_meanmax_pooling_of_a_pokemon_without_moves_is_zero():
    vec = torch.randn(2, 12, 4, 8)
    present = torch.ones(2, 12, 4, 1)
    present[0, 3] = 0
    pooled = MiniPokeTransformer.pool_moves(vec * present, present)
    assert pooled.shape == (2, 12, 16)
    assert torch.equal(pooled[0, 3], torch.zeros(16))
    assert torch.allclose(pooled[1, 0, :8], vec[1, 0].mean(0)) and torch.allclose(pooled[1, 0, 8:], vec[1, 0].amax(0))


def test_meanmax_pooling_is_smaller_on_large():   # mon_net reads 2d instead of 4d
    concat = sum(p.numel() for p in MiniPokeTransformer(preset("large", encoder_version=2)).parameters())
    pooled = sum(p.numel() for p in MiniPokeTransformer(preset("large", encoder_version=2,
                                                               move_pool="meanmax")).parameters())
    assert concat - pooled > 1_000_000


def test_unknown_move_pool_is_refused():
    with pytest.raises(ValueError, match="move_pool"):
        MiniPokeTransformer(preset("small", move_pool="sum"))


# --------------------------------------------------------------------------
# Opponent heads
# --------------------------------------------------------------------------
@pytest.mark.parametrize("cfg", [dict(), dict(encoder_version=2, history=True, move_effects=True),
                                 dict(encoder_version=2, move_tokens=True)])
def test_opp_heads_shapes_and_masks(cfg):
    model = make_model("small", opp_heads=True, **cfg).eval()
    obs = batch_for(model)
    with torch.no_grad():
        logits, value, _, out = model.forward_train(obs, opp=True)
    k = model.cfg.cand_moves if model.cfg.encoder_version == 2 else 0
    B = obs["mask"].shape[0]
    assert out["opp_act"].shape == (B, T.n_opp_actions(k)) == out["opp_act_mask"].shape
    assert (out["opp_act"][~out["opp_act_mask"]] <= -1e8).all()
    assert torch.isfinite(out["opp_act"][out["opp_act_mask"]]).all()
    present = obs["mon_feats"][:, 6:, 0] > 0
    assert torch.equal(out["opp_act_mask"][:, :6], present)
    assert out["opp_tera"].shape == (B,)
    assert out["opp_item"].shape == (B, 6, E.VOCAB_SIZES["item"])
    assert out["opp_ability"].shape == (B, 6, E.VOCAB_SIZES["ability"])
    assert out["opp_tera_type"].shape == (B, 6, T.N_TERA_TYPES)
    assert out["opp_moves"].shape == (B, 6, E.VOCAB_SIZES["move"])


def test_opp_heads_change_nothing_the_model_plays_and_are_not_exported(tmp_path):
    import onnx
    from export_onnx import export
    plain = make_model("small", encoder_version=2, history=True, move_effects=True).eval()
    with_heads = with_opp_heads(copy.deepcopy(plain)).eval()
    assert with_heads.cfg.opp_heads and not plain.cfg.opp_heads
    obs = batch_for(plain)
    with torch.no_grad():
        a, b = plain(obs), with_heads(obs)
    assert torch.equal(a[0], b[0]) and torch.equal(a[1], b[1])

    def weights(path):
        return sum(int(np.prod(t.dims)) for t in onnx.load(str(path)).graph.initializer)
    pa = export(plain, tmp_path / "plain")
    pb = export(with_heads, tmp_path / "heads")
    assert weights(pa) == weights(pb)
    assert (tmp_path / "plain" / "encoder_spec.json").read_text() == \
        (tmp_path / "heads" / "encoder_spec.json").read_text()


def test_a_checkpoint_with_opp_heads_reloads_with_them(tmp_path):
    from checkpoint import load_checkpoint, save_checkpoint
    model = make_model("small", opp_heads=True)
    save_checkpoint(tmp_path / "m.pt", model)
    loaded = build_model(load_checkpoint(tmp_path / "m.pt"))
    assert loaded.cfg.opp_heads
    assert all(torch.equal(v, loaded.state_dict()[k]) for k, v in model.state_dict().items())


def test_forward_train_refuses_opp_outputs_without_the_heads():
    model = make_model("small")
    with pytest.raises(ValueError, match="no opponent heads"):
        model.forward_train(batch_for(model), opp=True)


# --------------------------------------------------------------------------
# Targets from the stored re-simulations
# --------------------------------------------------------------------------
def test_targets_from_a_resimulation_name_what_the_opponent_really_did():
    import resim as R
    from tests.test_resim import STEMS, resim_of, stored
    encoders = R.parity_encoders(["MiniPokeV3_22M"])
    named = 0
    for stem in STEMS:
        res = resim_of(stem, stored(stem))
        for side in R.SIDES:
            opp = R.SIDES[1 - R.SIDES.index(side)]
            dec = R.side_decisions(res.sides[side], res.replay.name(side), encoders, stem)
            targets = T.side_targets(res, side, dec["decisions"])
            assert len(targets) == len(dec["decisions"])
            index_of = {r["line"]: i for i, r in enumerate(res.requests[side])}
            sets = T.real_sets(res.sides[opp], res.requests[opp])
            for d, t in zip(dec["decisions"], targets):
                obs = d["obs"]["MiniPokeV3_22M"]
                record = res.requests[opp][index_of[d["line"]]]
                act = int(t["opp_act"])
                if record["wait"]:
                    assert act == T.NO_TARGET
                    continue
                choice = R.as_choice(opp, record["choice"])
                request = T.request_at(res.sides[opp], record)
                if choice.kind == "switch":
                    ident = request["side"]["pokemon"][choice.position - 1]["ident"]
                    if ident in d["opponent_team"][:6]:
                        assert d["opponent_team"][act] == ident
                    else:
                        assert act == T.OPP_UNREVEALED
                elif act >= 0:
                    a = int(np.argmax(obs["mon_feats"][6:, 3]))
                    mid = E.MOVE_VOCAB[choice.move]
                    if act < T.OPP_CAND0:
                        assert obs["move_ids"][6 + a][act - T.OPP_KNOWN0] == mid
                    elif act < T.opp_other(obs["cand_ids"].shape[1]):
                        assert obs["cand_ids"][a][act - T.OPP_CAND0] == mid
                    assert int(t["opp_tera"]) in (T.NO_TARGET, int(choice.tera))
                named += act >= 0
                for j, key in enumerate(d["opponent_team"][:6]):
                    s = sets[key]
                    assert t["opp_item"][j] == T.item_target(s["item"])
                    assert t["opp_ability"][j] == E.ABILITY_VOCAB[s["baseAbility"]]
                    assert t["opp_tera_type"][j] == T.TERA_INDEX[s["teraType"].lower()]
                    assert sorted(int(m) for m in t["opp_moves"][j] if m) == \
                        sorted(E.MOVE_VOCAB[m] for m in s["moves"])
                assert (t["opp_item"][len(d["opponent_team"]):] == T.NO_TARGET).all()
    assert named > 30


def test_unpaired_requests_give_no_choice_targets():
    import resim as R
    from tests.test_resim import STEMS, resim_of, stored
    stem = STEMS[0]
    out = stored(stem)
    out["requests"]["p2"] = out["requests"]["p2"][:-1]
    res = resim_of(stem, out)
    dec = R.side_decisions(res.sides["p1"], res.replay.name("p1"), R.parity_encoders(["MiniPokeV3_22M"]), stem)
    targets = T.side_targets(res, "p1", dec["decisions"])
    assert all(int(t["opp_act"]) == T.NO_TARGET for t in targets)
    assert any((t["opp_item"] >= 0).any() for t in targets)        # the sets are still known


# --------------------------------------------------------------------------
# Training with them
# --------------------------------------------------------------------------
def write_split(root, n_traj, seed, first_traj=0, opp=True):
    rng = np.random.default_rng(seed)
    obs = random_obs(n_traj * 10, seed)
    writer = ShardWriter(root, shard_size=64, obs_layout=V1_LAYOUT,
                         extra=H.EXTRA if opp else {k: v for k, v in H.EXTRA.items() if k not in T.OPP_EXTRA})
    for t in range(n_traj):
        result = float(rng.choice([1.0, -1.0]))
        rewards = [0.0] * 9 + [result]
        rets = discounted_returns(rewards, 0.995)
        for s in range(10):
            o = obs[t * 10 + s]
            action = int(rng.choice(np.flatnonzero(o["mask"])))
            probs = np.zeros(14, dtype=np.float32)
            probs[action] = 1.0
            extra = dict(traj=first_traj + t, step=s, reward=rewards[s], done=int(s == 9), rating=1800,
                         turn=s + 1, label_ok=1, result=int(result))
            if opp:
                extra.update(opp_act=int(rng.integers(-1, T.n_opp_actions(0))), opp_tera=int(rng.integers(-1, 2)),
                             opp_item=rng.integers(-1, 50, 6), opp_ability=rng.integers(-1, 50, 6),
                             opp_tera_type=rng.integers(-1, T.N_TERA_TYPES, 6),
                             opp_moves=rng.integers(0, 50, (6, 4)))
            writer.add(o, probs, action, 0.0, float(rets[s]), **extra)
    writer.close()


def dataset(root, opp=True):
    for i, (split, n) in enumerate((("train", 24), ("val", 6), ("test", 6))):
        write_split(root / split, n, seed=i, first_traj=1000 * i, opp=opp)
    (root / "meta.json").write_text(json.dumps({"model_flags": {"encoder_version": 1},
                                                "set_priors": None, "gamma": 0.995}))
    return root


def train(root, tmp_path, model, **cfg):
    from checkpoint import save_checkpoint
    torch.manual_seed(0)
    config = O.OfflineConfig(minibatch=64, warmup_steps=2, **cfg)
    return O.offline_train(model, O.HumanData(root), config, "cpu", tmp_path, save_checkpoint,
                           log=lambda *_: None)


def test_offline_training_with_opp_heads_learns_them(tmp_path):
    root = dataset(tmp_path / "data")
    model = MiniPokeTransformer(preset("small", opp_heads=True, move_pool="meanmax"))
    result = train(root, tmp_path / "run", model, algo="awbc", epochs=4, patience=10, lr=3e-3, opp_coef=0.5)
    opp = [h["opp"] for h in result["history"]]
    assert opp[-1] < opp[0] and all(np.isfinite(opp))
    m = result["history"][-1]["val_opp"]
    assert 0 <= m["opp_act_top1"] <= 1 and m["opp_act_n"] > 0
    test = O.opp_metrics(model, O.HumanData(root)["test"], "cpu")
    assert set(test) >= {"opp_act_top1", "opp_act_nll", "opp_tera_acc", "item_top1_hidden",
                         "ability_top1_hidden", "tera_type_top1", "moves_recall4_hidden"}


def test_opp_losses_are_about_one_at_init():
    model = MiniPokeTransformer(preset("small", opp_heads=True)).eval()
    obs = batch_for(model, n=64)
    with torch.no_grad():
        _, _, _, out = model.forward_train(obs, opp=True)
    rng = np.random.default_rng(0)
    possible = out["opp_act_mask"].float()
    t = {"opp_act": torch.multinomial(possible / possible.sum(-1, keepdim=True), 1).squeeze(1), "opp_tera": torch.from_numpy(rng.integers(0, 2, 64)),
         "opp_item": torch.from_numpy(rng.integers(2, 1000, (64, 6))),
         "opp_ability": torch.from_numpy(rng.integers(2, 200, (64, 6))),
         "opp_tera_type": torch.from_numpy(rng.integers(0, 18, (64, 6))),
         "opp_moves": torch.from_numpy(rng.integers(2, 900, (64, 6, 4)))}
    losses = O.opp_losses(out, t)
    for k, v in losses.items():
        assert 0.3 < v.item() < 1.3, k


def test_opp_training_needs_the_heads_and_the_targets(tmp_path):
    with_targets = dataset(tmp_path / "with")
    without = dataset(tmp_path / "without", opp=False)
    with pytest.raises(ValueError, match="opponent heads"):
        train(with_targets, tmp_path / "a", MiniPokeTransformer(PRESETS["small"]), epochs=1, opp_coef=0.5)
    with pytest.raises(ValueError, match="no opponent targets"):
        train(without, tmp_path / "b", MiniPokeTransformer(preset("small", opp_heads=True)), epochs=1, opp_coef=0.5)
    # opp_coef 0: a model with the heads trains as before on a dataset without targets
    train(without, tmp_path / "c", MiniPokeTransformer(preset("small", opp_heads=True)), epochs=1)


def test_human_data_writes_the_opponent_targets_into_its_shards(tmp_path):
    """human_data.encode_side on a stored re-simulation, written and read back as the dataset."""
    from dataset import ShardDataset
    from tests.test_resim import STEMS, resim_of, stored
    stem = STEMS[0]
    res = resim_of(stem, stored(stem))
    encoder, _, _ = H.load_encoder("../../src/ElsaMina.Battles/Models/MiniPokeV3_22M")
    rows, stats = H.encode_side(res, "p1", encoder, 1.0, 0.1, 0.995)
    assert rows and stats["opp_act_targets"] == sum(int(r[-1]["opp_act"]) >= 0 for r in rows) > 0
    writer = ShardWriter(tmp_path, shard_size=64, obs_layout=encoder.layout, extra=H.EXTRA)
    for step, (obs, action, ok, reward, ret, done, turn, opp) in enumerate(rows):
        probs = np.zeros(14, dtype=np.float32)
        writer.add(obs, probs, action, 0.0, ret, traj=0, step=step, reward=reward, done=int(done),
                   rating=1800, turn=turn, label_ok=int(ok), result=1, **opp)
    writer.close()
    data = ShardDataset(tmp_path, val_frac=0.0)
    assert O.has_opp_targets(data)
    got = np.concatenate([mb["opp_act"][np.argsort(mb["row"])] for mb in data.batches("train", 1024)])
    assert got.tolist() == [int(r[-1]["opp_act"]) for r in rows]
