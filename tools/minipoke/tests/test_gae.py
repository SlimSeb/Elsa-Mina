import numpy as np
import pytest
import torch

from ppo import Schedule, compute_gae, explained_variance, two_hot, value_loss
from model import MiniPokeTransformer, PRESETS


def step(value, reward):
    return {"obs": None, "action": 0, "logp": 0.0, "value": value, "reward": reward}


def test_gae_matches_a_hand_computed_episode():
    gamma, lam = 0.9, 0.8
    ep = [step(0.5, 0.0), step(0.2, 0.1), step(-0.1, 1.0)]
    _, _, _, adv, ret, values = compute_gae([ep], gamma, lam)
    d2 = 1.0 + 0.0 - (-0.1)              # terminal: no bootstrap
    d1 = 0.1 + gamma * -0.1 - 0.2
    d0 = 0.0 + gamma * 0.2 - 0.5
    a2 = d2
    a1 = d1 + gamma * lam * a2
    a0 = d0 + gamma * lam * a1
    np.testing.assert_allclose(adv, [a0, a1, a2], rtol=1e-6)
    np.testing.assert_allclose(ret, adv + np.array([0.5, 0.2, -0.1]), rtol=1e-6)
    np.testing.assert_allclose(values, [0.5, 0.2, -0.1])


def test_gae_does_not_bootstrap_across_episodes():
    a = [step(0.0, 1.0)]
    b = [step(5.0, 0.0)]
    _, _, _, adv, _, _ = compute_gae([a, b], 0.99, 0.95)
    np.testing.assert_allclose(adv, [1.0, -5.0])


def test_gae_with_lambda_one_gives_monte_carlo_returns():
    ep = [step(0.3, 0.1), step(0.1, 0.0), step(0.7, -1.0)]
    _, _, _, _, ret, _ = compute_gae([ep], 0.5, 1.0)
    np.testing.assert_allclose(ret, [0.1 + 0.5 * 0 + 0.25 * -1, 0.0 + 0.5 * -1, -1.0], rtol=1e-6)


SUPPORT = torch.linspace(-1.6, 1.6, 51)


@pytest.mark.parametrize("target", [-1.6, -1.0, -0.0371, 0.0, 0.5, 1.23, 1.6])
def test_two_hot_expectation_is_the_target(target):
    dist = two_hot(torch.tensor([target]), SUPPORT)
    assert dist.sum().item() == pytest.approx(1.0)
    assert (dist @ SUPPORT).item() == pytest.approx(target, abs=1e-6)
    assert (dist > 0).sum() <= 2


def test_two_hot_clamps_returns_outside_the_support():
    dist = two_hot(torch.tensor([3.0, -9.0]), SUPPORT)
    assert dist[0, -1] == 1.0 and dist[1, 0] == 1.0


def test_two_hot_of_gae_returns_round_trips():
    ep = [step(0.1, 0.1), step(0.2, 0.1), step(0.3, 1.0)]
    _, _, _, _, ret, _ = compute_gae([ep], 0.995, 0.95)
    r = torch.from_numpy(ret)
    torch.testing.assert_close(two_hot(r, SUPPORT) @ SUPPORT, r)


def test_distributional_value_loss_is_minimised_by_the_two_hot_target():
    torch.manual_seed(0)
    model = MiniPokeTransformer(PRESETS["medium"])
    targets = torch.tensor([0.37, -0.8])
    dist = two_hot(targets, model.value_support)
    good = torch.log(dist.clamp_min(1e-12))
    worse = torch.zeros_like(good)
    values = (dist @ model.value_support)
    assert value_loss(model, values, good, targets) < value_loss(model, values, worse, targets)
    # gradient of CE wrt logits = softmax - target
    logits = torch.zeros(2, 51, requires_grad=True)
    value_loss(model, values, logits, targets).backward()
    torch.testing.assert_close(logits.grad, (torch.softmax(logits, -1) - dist).detach() / 2)


def test_explained_variance():
    r = np.array([1.0, -1.0, 0.5, 0.0])
    assert explained_variance(r, r) == pytest.approx(1.0)
    assert explained_variance(np.zeros(4), r) == pytest.approx(0.0)


def test_constant_schedule_is_the_v1_recipe():
    s = Schedule(lr=2e-4)
    assert [s.lr_at(i, p) for i, p in [(1, 0.0), (50, 0.4), (300, 1.0)]] == [2e-4] * 3
    assert s.ent_at(0.0) == s.ent_at(1.0) == 0.01


def test_warmup_then_cosine_and_entropy_annealing():
    s = Schedule(lr=1e-3, warmup_iters=10, lr_min_ratio=0.1, ent_start=0.01, ent_end=0.003)
    assert s.lr_at(1, 0.0) == pytest.approx(1e-4)
    assert s.lr_at(10, 0.01) == pytest.approx(1e-3)
    assert s.lr_at(11, 0.5) == pytest.approx(1e-3 * (0.1 + 0.9 * 0.5))
    assert s.lr_at(500, 1.0) == pytest.approx(1e-4)
    assert s.ent_at(0.5) == pytest.approx(0.0065)
    assert s.ent_at(2.0) == pytest.approx(0.003)
