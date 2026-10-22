import numpy as np
import pytest

from dataset import ShardDataset, ShardWriter, discounted_returns
from tests.helpers import random_obs


def fill(root, n, shard_size):
    obs = random_obs(n, seed=3)
    rng = np.random.default_rng(0)
    rows = []
    w = ShardWriter(root, shard_size)
    for i, o in enumerate(obs):
        probs = rng.dirichlet(np.ones(14)).astype(np.float32) * o["mask"]
        probs /= probs.sum()
        rows.append((o, probs, i % 14, float(rng.uniform(-1, 1)), float(rng.uniform(-1.6, 1.6))))
        w.add(o, probs, i % 14, rows[-1][3], rows[-1][4])
    w.close()
    return rows


def test_shards_round_trip(tmp_path):
    rows = fill(tmp_path, 250, shard_size=64)
    ds = ShardDataset(tmp_path, val_frac=0.0)
    assert len(ds) == 250 and len(ds.shards) == 4
    got = list(ds.batches("train", 1000))
    assert len(got) == 1
    b = got[0]
    for i, (o, probs, action, value, ret) in enumerate(rows):
        for k in ("mon_ids", "move_ids", "mask"):
            np.testing.assert_array_equal(b["obs"][k][i], o[k])
        for k in ("mon_feats", "move_feats", "field"):
            np.testing.assert_allclose(b["obs"][k][i], o[k], rtol=1e-3, atol=1e-3)
        np.testing.assert_allclose(b["probs"][i], probs, atol=2e-3)
        assert b["action"][i] == action
        assert b["value"][i] == pytest.approx(value, abs=2e-3)
        assert b["ret"][i] == pytest.approx(ret, abs=2e-3)
    assert b["obs"]["mon_ids"].dtype == np.int64 and b["obs"]["mask"].dtype == bool
    assert b["obs"]["mon_feats"].dtype == np.float32


def test_streaming_loader_visits_each_train_sample_once_shuffled(tmp_path):
    fill(tmp_path, 300, shard_size=50)
    ds = ShardDataset(tmp_path, val_frac=0.1, buffer_shards=2)
    rng = np.random.default_rng(0)
    seen = np.concatenate([b["action"] * 1000 + (b["ret"] * 100).round() for b in ds.batches("train", 32, rng)])
    assert len(seen) == ds.n_samples("train") == 300 - 6 * 5
    ordered = np.concatenate([b["action"] * 1000 + (b["ret"] * 100).round() for b in ds.batches("train", 32)])
    assert sorted(seen) == sorted(ordered)
    assert not np.array_equal(seen, ordered)
    assert ds.n_samples("val") == 30
    assert sum(len(b["action"]) for b in ds.batches("val", 7)) == 30


def test_shards_are_compact(tmp_path):
    fill(tmp_path, 200, shard_size=200)
    ds = ShardDataset(tmp_path)
    assert ds.bytes_on_disk() / len(ds) < 6200  # float32 dicts: ~12 KB per sample


def test_writer_refuses_to_overwrite_a_dataset(tmp_path):
    fill(tmp_path, 3, shard_size=2)
    with pytest.raises(FileExistsError):
        ShardWriter(tmp_path)


def test_discounted_returns():
    np.testing.assert_allclose(discounted_returns([0.1, 0.0, 1.0], 0.5), [0.1 + 0.25, 0.5, 1.0])
