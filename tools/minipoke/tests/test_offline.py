"""minipoke v3, part B: the human replay dataset (dataset.py extra fields, human_data.py helpers)
and offline RL (offline.py: GAE along trajectories, advantage weights, bc / awbc / q training,
the offline metrics, the ONNX policy). Synthetic data, CPU, no Showdown."""

import json
from pathlib import Path

import numpy as np
import pytest
import torch

import human_data as H
import offline as O
from dataset import ShardDataset, ShardWriter, discounted_returns
from encoder_v2 import V1_LAYOUT
from model import MiniPokeTransformer, PRESETS
from ppo import compute_gae
from tests.helpers import random_obs

GAMMA = 0.995


# --------------------------------------------------------------------------
# A synthetic human dataset: V1 observations, trajectories of 10 decisions
# --------------------------------------------------------------------------
def write_split(root: Path, n_traj: int, seed: int, first_traj: int = 0) -> None:
    rng = np.random.default_rng(seed)
    obs = random_obs(n_traj * 10, seed)
    writer = ShardWriter(root, shard_size=64, obs_layout=V1_LAYOUT, extra=H.EXTRA)
    for t in range(n_traj):
        result = float(rng.choice([1.0, -1.0]))
        rewards = [float(rng.choice([0.0, 0.1, -0.1])) for _ in range(10)]
        rewards[-1] += result
        rets = discounted_returns(rewards, GAMMA)
        rating = int(rng.choice([1550, 1800]))
        for s in range(10):
            o = obs[t * 10 + s]
            legal = np.flatnonzero(o["mask"])
            ok = s != 9 or t % 3          # some last decisions unanswered
            action = int(rng.choice(legal)) if ok else -1
            probs = np.zeros(len(o["mask"]), dtype=np.float32)
            if ok:
                probs[action] = 1.0
            writer.add(o, probs, action, 0.0, float(rets[s]), traj=first_traj + t, step=s,
                       reward=rewards[s], done=int(s == 9), rating=rating, turn=s + 1,
                       label_ok=int(ok), result=int(result))
    writer.close()


@pytest.fixture
def human_data(tmp_path):
    root = tmp_path / "human"
    for i, (split, n) in enumerate((("train", 24), ("val", 6), ("test", 6))):
        write_split(root / split, n, seed=i, first_traj=1000 * i)
    (root / "meta.json").write_text(json.dumps({"model_flags": {"encoder_version": 1},
                                                "set_priors": None, "gamma": GAMMA}))
    return root


# --------------------------------------------------------------------------
# The dataset's extra fields
# --------------------------------------------------------------------------
def test_extra_fields_round_trip_with_global_row_indices(human_data):
    data = ShardDataset(human_data / "train", val_frac=0.0)
    assert len(data) == 240 and len(data.shards) == 4
    rows = np.concatenate([mb["row"] for mb in data.batches("train", 50)])
    assert np.array_equal(rows, np.arange(240))
    steps = data.column("step")
    assert np.array_equal(steps[:10], np.arange(10)) and steps.dtype == np.int16
    mb = next(data.batches("train", 32, rng=np.random.default_rng(0)))
    assert mb["traj"].dtype == np.int64 and mb["reward"].dtype == np.float32
    assert np.array_equal(data.column("step")[mb["row"]], mb["step"])


def test_datasets_without_extra_fields_read_as_before(tmp_path):
    writer = ShardWriter(tmp_path / "plain", shard_size=8)
    for o in random_obs(10):
        writer.add(o, np.ones(14, dtype=np.float32) / 14, 0)
    writer.close()
    data = ShardDataset(tmp_path / "plain")
    meta = json.loads((tmp_path / "plain" / "meta.json").read_text())
    assert "extra_targets" not in meta and data.extra == {}
    assert set(next(data.batches("train", 4))) == {"obs", "probs", "value", "ret", "action", "row"}


# --------------------------------------------------------------------------
# human_data.py helpers
# --------------------------------------------------------------------------
def test_side_rewards_follow_players_trajectories():
    # fainted (ours, theirs) at each decision; final (1, 3); a win
    rewards = H.side_rewards([(0, 0), (0, 1), (1, 1)], (1, 3), 1.0, 0.1)
    assert rewards == pytest.approx([0.1, -0.1, 0.2 + 1.0])


def test_outcome_reads_the_winner_by_name():
    names = {"p1": "Some One", "p2": "other"}
    assert H.outcome(["|turn|3", "|win|Some One"], names) == {"p1": 1.0, "p2": -1.0}
    assert H.outcome(["|tie"], names) == {"p1": 0.0, "p2": 0.0}


def test_splits_by_upload_time_put_the_newest_battles_in_test():
    times = [5, 1, 9, 3, 7, 2, 8, 4, 6, 10]
    splits = H.assign_splits(times, val_frac=0.2, test_frac=0.2)
    assert {t for t, s in zip(times, splits) if s == "test"} == {9, 10}
    assert {t for t, s in zip(times, splits) if s == "val"} == {7, 8}


def test_a_stopped_build_resumes_its_finished_chunks_only(tmp_path):
    tasks = [{"chunk": k, "battles": [{"path": f"r{k}.json.gz"}]} for k in range(3)]
    assert H.resume_chunks(tmp_path / "d", tasks, {"gamma": GAMMA}) == {}
    H.save_chunk(tmp_path / "d", 1, {"shards": {}, "stats": {"x": 1}, "records": []})
    (tmp_path / "d" / "train" / "c00002").mkdir(parents=True)          # half written
    done = H.resume_chunks(tmp_path / "d", tasks, {"gamma": GAMMA})
    assert list(done) == [1] and not (tmp_path / "d" / "train" / "c00002").exists()
    with pytest.raises(SystemExit, match="other replays or settings"):
        H.resume_chunks(tmp_path / "d", tasks, {"gamma": 0.9})


# --------------------------------------------------------------------------
# Advantages
# --------------------------------------------------------------------------
def test_gae_matches_ppos_on_one_trajectory_whatever_the_row_order():
    rng = np.random.default_rng(0)
    values = rng.uniform(-1, 1, 12).astype(np.float32)
    rewards = rng.choice([0.0, 0.1, -0.1], 12).astype(np.float32)
    rewards[-1] += 1.0
    episode = [{"obs": {}, "action": 0, "logp": 0.0, "value": float(v), "reward": float(r)}
               for v, r in zip(values, rewards)]
    _, _, _, expected, _, _ = compute_gae([episode], GAMMA, 0.95)
    perm = rng.permutation(12)
    dones = np.zeros(12)
    dones[-1] = 1
    out = O.gae(values[perm], rewards[perm], dones[perm], np.zeros(12, int), np.arange(12)[perm], GAMMA, 0.95)
    assert out["adv"] == pytest.approx(expected[perm], abs=1e-5)
    assert out["next_value"][perm == 11][0] == 0.0
    assert out["next_value"][perm == 3][0] == pytest.approx(values[4])


def test_gae_cuts_trajectories_at_a_new_id_or_a_missing_step():
    values = np.ones(4, dtype=np.float32)
    out = O.gae(values, np.zeros(4, np.float32), np.zeros(4), np.array([0, 0, 1, 1]),
                np.array([0, 2, 0, 1]), 1.0, 1.0)
    # rows 0 and 1 are not consecutive steps: both end a segment (no bootstrap), like row 3
    assert out["next_value"].tolist() == [0.0, 0.0, 1.0, 0.0]


def test_advantage_weights():
    adv = np.array([-2.0, 0.0, 1.0, 50.0], dtype=np.float32)
    labelled = np.array([True, True, True, False])
    assert O.advantage_weights(adv, labelled, O.OfflineConfig(weight="binary")).tolist() == [0, 0, 1, 0]
    w = O.advantage_weights(adv, labelled, O.OfflineConfig(weight="exp", beta=1.0, max_weight=20.0))
    std = adv[labelled].std()
    assert w[:3] == pytest.approx(np.minimum(np.exp(adv[:3] / std), 20.0)) and w[3] == 0.0


def test_auc():
    assert O.auc(np.array([0.1, 0.4, 0.35, 0.8]), np.array([0, 0, 1, 1])) == 0.75
    assert O.auc(np.array([0.5, 0.5]), np.array([0, 1])) == 0.5


# --------------------------------------------------------------------------
# Training end to end
# --------------------------------------------------------------------------
def run(human_data, tmp_path, **cfg):
    from checkpoint import save_checkpoint
    from model import with_q_head
    torch.manual_seed(0)
    model = MiniPokeTransformer(PRESETS["small"])
    if cfg.get("algo") == "q":
        model = with_q_head(model)
    data = O.HumanData(human_data)
    config = O.OfflineConfig(minibatch=64, warmup_steps=2, **cfg)
    out = tmp_path / cfg.get("algo", "bc")
    out.mkdir()
    result = O.offline_train(model, data, config, "cpu", out,
                             lambda path, m, **extra: save_checkpoint(path, m, None, **extra),
                             log=lambda *_: None)
    return result, out


@pytest.mark.parametrize("algo, weight", [("bc", "exp"), ("awbc", "exp"), ("awbc", "binary"), ("q", "exp")])
def test_offline_training_runs_and_keeps_the_best_epoch(human_data, tmp_path, algo, weight):
    result, out = run(human_data, tmp_path, algo=algo, weight=weight, epochs=3, warmup_epochs=1,
                      patience=5, lr=3e-3)
    assert len(result["history"]) == 3 and (out / "offline.pt").exists()
    from checkpoint import load_checkpoint
    best = load_checkpoint(out / "offline.pt")
    assert best["offline"]["epoch"] == result["best_epoch"]
    assert best["offline"]["val"]["nll"] == pytest.approx(result["best_val_nll"])
    weighted = [h for h in result["history"] if not np.isnan(h["w_mean"])]
    assert len(weighted) == (2 if algo != "bc" else 0)    # after the warmup epoch
    if algo == "q":
        assert best["cfg"]["q_head"] and all(h["q"] > 0 for h in result["history"])


def test_offline_training_stops_when_validation_nll_stops_improving(human_data, tmp_path):
    result, _ = run(human_data, tmp_path, algo="bc", epochs=30, patience=1, lr=5e-2)
    assert len(result["history"]) < 30
    nll = [h["val_nll"] for h in result["history"]]
    assert result["best_val_nll"] == min(nll) and nll[-1] >= min(nll)


def test_offline_metrics(human_data):
    torch.manual_seed(0)
    model = MiniPokeTransformer(PRESETS["small"])
    data = O.HumanData(human_data)
    m = O.offline_metrics(model, data["test"], "cpu", min_rating=1700)
    labelled = int((data["test"].column("label_ok") > 0).sum())
    strong = int(((data["test"].column("label_ok") > 0) & (data["test"].column("rating") >= 1700)).sum())
    assert m["all"]["n"] == labelled and m["strong"]["n"] == strong
    assert 0 <= m["all"]["top1"] <= 1 and m["all"]["nll"] > 0
    assert 0 <= m["value"]["auc"] <= 1
    assert sum(c["n"] for c in m["value"]["calibration"]) == len(data["test"])
    assert "calibration" in O.describe_metrics(m)


# --------------------------------------------------------------------------
# A released model as a policy
# --------------------------------------------------------------------------
RELEASED = Path(__file__).resolve().parents[3] / "src" / "ElsaMina.Battles" / "Models" / "MiniPokeV1_1M"


@pytest.mark.skipif(not O.is_onnx_folder(RELEASED), reason="ElsaMina's MiniPokeV1_1M not found")
def test_onnx_policy_plays_like_a_model(human_data):
    from encoder_v2 import model_encoder
    from players import InferenceBatcher
    from rollout import frozen_copy
    import train
    policy = train.load_policy(RELEASED)
    assert isinstance(policy, O.OnnxPolicy) and "ONNX" in train.describe_policy(policy)
    copy = frozen_copy(policy, torch.device("cpu"))
    assert model_encoder(copy).layout == dict(V1_LAYOUT)
    batcher = InferenceBatcher(copy, torch.device("cpu"))
    out = batcher._forward([(o, True, None) for o in random_obs(3)])
    assert len(out) == 3 and all(0 <= a < 14 for a, _, _, _ in out)
    m = O.offline_metrics(policy, O.HumanData(human_data)["test"], "cpu")
    assert m["all"]["n"] > 0
