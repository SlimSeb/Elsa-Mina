"""Resuming must continue exactly where the run stopped: same weights,
optimizer moments, RNG streams -> identical losses on the next update."""

import random

import numpy as np
import pytest
import torch

from checkpoint import load_checkpoint, rng_state, save_checkpoint, seed_everything, set_rng_state
from model import MiniPokeTransformer, PRESETS, build_model
from ppo import PPOConfig, ppo_update
from tests.helpers import random_episodes

CFG = PPOConfig(minibatch=64, epochs=2)


def make(size):
    torch.manual_seed(1)
    model = MiniPokeTransformer(PRESETS[size])
    return model, torch.optim.AdamW(model.parameters(), lr=3e-4, weight_decay=1e-2)


@pytest.mark.parametrize("size", ["small", "medium"])
def test_resume_gives_identical_next_step_losses(size, tmp_path):
    episodes1, episodes2 = random_episodes(6, 20, seed=1), random_episodes(6, 20, seed=2)
    seed_everything(123)
    model, opt = make(size)
    ppo_update(model, opt, episodes1, CFG, "cpu")
    save_checkpoint(tmp_path / "ck.pt", model, opt, rng=rng_state(), state={"iteration": 1})
    expected = ppo_update(model, opt, episodes2, CFG, "cpu")
    after_random = (random.random(), np.random.rand(), torch.rand(1).item())

    # A fresh process: different RNG state, new objects, then resume
    seed_everything(999)
    ck = load_checkpoint(tmp_path / "ck.pt")
    model2 = build_model(ck)
    opt2 = torch.optim.AdamW(model2.parameters(), lr=3e-4, weight_decay=1e-2)
    opt2.load_state_dict(ck["opt"])
    set_rng_state(ck["rng"])
    got = ppo_update(model2, opt2, episodes2, CFG, "cpu")

    for k in ("pi_loss", "v_loss", "entropy", "kl", "clipfrac"):
        assert got[k] == expected[k], k
    for a, b in zip(model.state_dict().values(), model2.state_dict().values()):
        assert torch.equal(a, b)
    assert (random.random(), np.random.rand(), torch.rand(1).item()) == after_random
    assert ck["state"] == {"iteration": 1}


def test_checkpoint_write_is_atomic(tmp_path):
    model, opt = make("small")
    save_checkpoint(tmp_path / "ck.pt", model, opt)
    assert [p.name for p in tmp_path.iterdir()] == ["ck.pt"]


def test_a_lone_leftover_sample_does_not_poison_the_weights():
    # 2 episodes x 20 steps + 1 = 41 states with minibatch 40 -> last minibatch of 1
    episodes = random_episodes(2, 20, seed=3) + [random_episodes(1, 1, seed=4)[0]]
    model, opt = make("medium")
    stats = ppo_update(model, opt, episodes, PPOConfig(minibatch=40, epochs=2), "cpu")
    assert all(torch.isfinite(p).all() for p in model.parameters())
    assert stats["skipped"] == 0 and np.isfinite(stats["pi_loss"])
