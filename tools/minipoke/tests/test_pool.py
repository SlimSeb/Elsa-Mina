import random

import pytest
import torch

from league import OpponentPool, split_battles, split_evenly
from model import MiniPokeTransformer, PRESETS


@pytest.fixture(scope="module")
def model():
    return MiniPokeTransformer(PRESETS["small"])


def test_pool_is_capped_and_keeps_newest_oldest_and_a_spread(model, tmp_path):
    pool = OpponentPool(tmp_path, cap=8, keep_recent=3)
    for it in range(0, 400, 10):
        pool.add(model, it)
    its = [s.iteration for s in pool.snapshots]
    assert len(its) == 8
    assert its[0] == 0                       # oldest kept
    assert its[-3:] == [370, 380, 390]       # newest kept
    # Older snapshots roughly evenly spaced. (The one just out of the recent block
    # is always next to it, so the last gap is not part of the spread.)
    old = its[:-3]
    gaps = [b - a for a, b in zip(old, old[1:])]
    assert max(gaps) <= 2 * min(gaps)
    assert sorted(p.name for p in tmp_path.glob("*.pt")) == [f"snap_{i:05d}.pt" for i in its]


def test_pool_round_trips_its_state_and_weights(model, tmp_path):
    pool = OpponentPool(tmp_path, cap=5)
    for it in (0, 10, 20):
        pool.add(model, it)
    other = OpponentPool(tmp_path, cap=5)
    other.load_state_dict(pool.state_dict())
    assert other.snapshots == pool.snapshots
    w = other.load_weights(other.snapshots[0])
    assert all(torch.equal(w[k], v) for k, v in model.state_dict().items())


def test_pool_state_skips_snapshots_missing_on_disk(model, tmp_path):
    pool = OpponentPool(tmp_path)
    pool.add(model, 0)
    pool.add(model, 10)
    (tmp_path / "snap_00000.pt").unlink()
    other = OpponentPool(tmp_path)
    other.load_state_dict(pool.state_dict())
    assert [s.iteration for s in other.snapshots] == [10]


def test_pool_sampling_is_distinct_and_covers_the_pool(model, tmp_path):
    pool = OpponentPool(tmp_path, cap=10)
    for it in range(0, 50, 10):
        pool.add(model, it)
    rng = random.Random(0)
    seen = set()
    for _ in range(200):
        s = pool.sample(2, rng)
        assert len({x.iteration for x in s}) == 2
        seen |= {x.iteration for x in s}
    assert seen == {0, 10, 20, 30, 40}
    assert len(pool.sample(9, rng)) == 5
    assert [s.iteration for s in pool.recent(3)] == [40, 30, 20]


@pytest.mark.parametrize("n", [1, 3, 7, 128, 192, 1000])
def test_battle_split_adds_up_and_follows_the_shares(n):
    m, p, h = split_battles(n, 0.7, 0.2, 0.1)
    assert m + p + h == n
    if n >= 100:
        assert abs(m / n - 0.7) < 0.02 and abs(p / n - 0.2) < 0.02 and abs(h / n - 0.1) < 0.02
    if n >= 3:
        assert min(m, p, h) >= 1


def test_v1_split_is_pure_mirror():
    assert split_battles(128, 1.0, 0.0, 0.0) == (128, 0, 0)


def test_split_evenly():
    assert split_evenly(25, 2) == [13, 12]
    assert sum(split_evenly(7, 3)) == 7
