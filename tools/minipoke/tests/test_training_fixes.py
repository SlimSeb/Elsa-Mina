"""Part B: mean-KL early stopping, faint reward annealing, and the defaults that
keep small on the V1 recipe."""

import numpy as np
import pytest
import torch

import train
from model import MiniPokeTransformer, PRESETS
from players import FAINT_REWARD, Rewards, Trajectories, faint_reward
from ppo import PPOConfig, kl_early_stop, ppo_update
from tests.helpers import random_episodes


def test_early_stop_uses_the_epoch_mean_kl():
    kls = [0.08, 0.05, 0.0]       # last minibatch says go on, the epoch says stop
    assert kl_early_stop(kls, 0.03, "mean")
    assert not kl_early_stop(kls, 0.03, "last")
    kls = [0.0, 0.01, 0.05]       # one noisy last minibatch: V1 stops, the mean does not
    assert not kl_early_stop(kls, 0.03, "mean")
    assert kl_early_stop(kls, 0.03, "last")
    assert not kl_early_stop([], 0.03, "mean") and not kl_early_stop([1.0], 0.0, "mean")


def test_ppo_update_stops_after_the_epoch_whose_mean_kl_is_too_high():
    torch.manual_seed(0)
    np.random.seed(0)
    model = MiniPokeTransformer(PRESETS["small"])
    opt = torch.optim.AdamW(model.parameters(), lr=5e-2)  # big steps -> big KL
    episodes = random_episodes(8, 20, seed=5)
    stats = ppo_update(model, opt, episodes, PPOConfig(minibatch=32, epochs=4, target_kl=1e-4,
                                                       kl_stop="mean"), "cpu")
    assert stats["epochs"] == 1
    stats = ppo_update(model, opt, episodes, PPOConfig(minibatch=32, epochs=4, target_kl=0.0,
                                                       kl_stop="mean"), "cpu")
    assert stats["epochs"] == 4


class FakeBattle:
    def __init__(self, tag="b1"):
        self.battle_tag = tag
        self.team = {}
        self.opponent_team = {}
        self.won = self.lost = False


def test_faint_reward_annealing():
    assert faint_reward(10**9, None) == faint_reward(0, 0) == FAINT_REWARD
    assert faint_reward(0, 1000) == FAINT_REWARD
    assert faint_reward(250, 1000) == pytest.approx(0.75 * FAINT_REWARD)
    assert faint_reward(5000, 1000) == 0.0


def test_trajectories_use_the_shared_reward_settings():
    class Mon:
        def __init__(self, fainted):
            self.fainted = fainted
    rewards = Rewards()
    traj = Trajectories(rewards)
    b = FakeBattle()
    traj.step(b, {})
    b.opponent_team = {"x": Mon(True)}
    rewards.faint = 0.05  # annealed between iterations
    traj.step(b, {})
    b.won = True
    out = traj.finish(b)
    assert [s["reward"] for s in out] == [pytest.approx(0.05), pytest.approx(1.0)]
    assert Rewards() == Rewards(win=1.0, loss=-1.0, faint=0.1)  # V1


def test_small_keeps_the_v1_recipe_and_medium_gets_the_fixes():
    def resolved(*argv):
        args = train.build_parser().parse_args(["ppo", *argv])
        train.resolve(args, args.size, train.PPO_KEYS)
        return args
    small = resolved("--size", "small")
    assert (small.kl_stop, small.pool_sampling, small.vf_coef, small.faint_anneal_states) == (
        "last", "uniform", 0.5, None)
    medium = resolved("--size", "medium")
    assert (medium.kl_stop, medium.pool_sampling, medium.value_ce_norm, medium.vf_coef) == (
        "mean", "pfsp", "log-bins", 0.5)
    raw = resolved("--size", "medium", "--value-ce-norm", "none")
    assert raw.vf_coef == 0.1
    assert resolved("--size", "medium", "--value-ce-norm", "none", "--vf-coef", "0.3").vf_coef == 0.3
    wide = resolved("--size", "wide")
    assert (wide.kl_stop, wide.pool_sampling, wide.lr) == ("last", "uniform", 2e-4)
    wide_medium = resolved("--size", "wide-medium")
    assert all(getattr(wide_medium, k) == getattr(medium, k) for k in train.DEFAULTS["medium"])


# --------------------------------------------------------------------------
# D5: win/loss reward and the uniform-KL exploration term
# --------------------------------------------------------------------------
def test_winloss_rewards():
    class Mon:
        def __init__(self, fainted):
            self.fainted = fainted
    traj = Trajectories(Rewards.named("winloss"))
    b = FakeBattle()
    traj.step(b, {})
    b.opponent_team = {"x": Mon(True)}
    traj.step(b, {})
    b.lost = True
    assert [s["reward"] for s in traj.finish(b)] == [0.0, 0.0]
    traj.step(b, {})
    b.lost, b.won = False, True
    assert [s["reward"] for s in traj.finish(b)] == [1.0]
    assert Rewards.named("shaped") == Rewards()


def test_uniform_kl():
    from ppo import uniform_kl
    mask = torch.tensor([[True, True, False, True], [True, False, False, False]])
    flat = torch.zeros(2, 4).masked_fill(~mask, -1e9)
    assert uniform_kl(flat, mask).item() == pytest.approx(0.0, abs=1e-6)
    peaked = torch.tensor([[5.0, 0.0, 0.0, 0.0], [3.0, 0.0, 0.0, 0.0]]).masked_fill(~mask, -1e9)
    logq = torch.log_softmax(peaked[0], -1)[[0, 1, 3]]
    expected = (-logq.mean() - np.log(3)).item() / 2   # row 2 has one legal action: KL 0
    assert uniform_kl(peaked, mask).item() == pytest.approx(expected, rel=1e-5)


def test_uniform_kl_term_raises_the_policy_entropy():
    episodes = random_episodes(8, 20, seed=7)
    entropies = []
    for coef in (0.0, 1.0):
        torch.manual_seed(0)
        np.random.seed(0)
        model = MiniPokeTransformer(PRESETS["small"])
        opt = torch.optim.AdamW(model.parameters(), lr=1e-3)
        for _ in range(3):
            stats = ppo_update(model, opt, episodes, PPOConfig(minibatch=64, epochs=2,
                                                               target_kl=0.0,
                                                               uniform_kl_coef=coef), "cpu")
        entropies.append(stats["entropy"])
        assert (stats["uniform_kl"] > 0) == (coef > 0)
    assert entropies[1] > entropies[0]


def test_reward_defaults_keep_v1():
    args = train.build_parser().parse_args(["ppo", "--size", "small"])
    train.resolve(args, "small", train.PPO_KEYS)
    assert args.reward == "shaped" and args.uniform_kl_coef == 0.0
