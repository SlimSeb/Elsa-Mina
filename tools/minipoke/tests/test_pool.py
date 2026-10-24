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


def test_pfsp_favours_the_snapshots_the_learner_beats_least(model, tmp_path):
    pool = OpponentPool(tmp_path, cap=10, sampling="pfsp", floor=0.1, ema=0.05)
    for it in (0, 10, 20):
        pool.add(model, it)
    pool.record(0, wins=100, games=100)   # always beaten -> weight at the floor
    pool.record(10, wins=50, games=100)
    pool.record(20, wins=0, games=100)    # never beaten
    weights = {s.iteration: pool.weight(s) for s in pool.snapshots}
    assert weights[0] == pytest.approx(0.1)
    assert weights[10] == pytest.approx(0.5, abs=0.01)
    assert weights[20] == pytest.approx(1.0, abs=0.01)
    rng = random.Random(0)
    counts = {0: 0, 10: 0, 20: 0}
    for _ in range(4000):
        counts[pool.sample(1, rng)[0].iteration] += 1
    total = sum(weights.values())
    for it, w in weights.items():
        assert counts[it] / 4000 == pytest.approx(w / total, abs=0.03)
    # every snapshot can still be drawn, and draws stay distinct
    assert counts[0] > 0
    assert all(len({s.iteration for s in pool.sample(3, rng)}) == 3 for _ in range(50))


def test_pool_win_rate_is_an_ema_over_games(model, tmp_path):
    pool = OpponentPool(tmp_path, ema=0.1)
    pool.add(model, 0)
    snap = pool.snapshots[0]
    assert (snap.win_rate, snap.games) == (0.5, 0)
    pool.record(0, wins=1, games=1)
    assert snap.win_rate == pytest.approx(0.55)
    pool.record(0, wins=0, games=10)    # 10 games of weight 0.1 toward 0
    assert snap.win_rate == pytest.approx(0.55 * 0.9 ** 10)
    assert snap.games == 11
    pool.record(99, wins=3, games=4)    # unknown snapshot (evicted): ignored
    other = OpponentPool(tmp_path)
    other.load_state_dict(pool.state_dict())
    assert other.snapshots[0].win_rate == snap.win_rate and other.snapshots[0].games == 11


def test_uniform_sampling_draws_like_v1(model, tmp_path):
    pool = OpponentPool(tmp_path, sampling="uniform")
    for it in range(0, 50, 10):
        pool.add(model, it)
    pool.record(0, wins=0, games=50)  # win rates do not matter
    a, b = random.Random(7), random.Random(7)
    assert pool.sample(2, a) == b.sample(pool.snapshots, 2)


def test_old_pool_states_load_with_neutral_win_rates(model, tmp_path):
    pool = OpponentPool(tmp_path)
    pool.add(model, 0)
    old_state = {"snapshots": [{"iteration": 0, "file": "snap_00000.pt"}]}
    pool.load_state_dict(old_state)
    assert (pool.snapshots[0].win_rate, pool.snapshots[0].games) == (0.5, 0)
