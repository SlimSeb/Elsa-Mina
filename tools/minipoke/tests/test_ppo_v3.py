"""minipoke v3, part A: the PPO step-size options (epochs, target KL), the reference-policy KL
anchor, recipes, and fixed pool opponents."""

import copy

import numpy as np
import pytest
import torch

import train
from checkpoint import load_checkpoint, save_checkpoint
from league import OpponentPool
from model import MiniPokeTransformer, PRESETS
from ppo import PPOConfig, policy_kl, ppo_update
from tests.helpers import random_episodes
from tests.test_ppo_loop import FakeRollouts, run_ppo  # noqa: F401 - the fixture


# --------------------------------------------------------------------------
# The reference KL
# --------------------------------------------------------------------------
def test_policy_kl_is_kl_over_the_legal_actions():
    mask = torch.tensor([[True, True, False, True]])
    logits = torch.tensor([[1.0, 0.0, -1e9, -1.0]])
    ref = torch.tensor([[0.0, 0.5, -1e9, 0.0]])
    p = torch.softmax(logits[:, [0, 1, 3]], -1)
    q = torch.softmax(ref[:, [0, 1, 3]], -1)
    expected = (p * (p.log() - q.log())).sum()
    assert policy_kl(logits, ref, mask).item() == pytest.approx(expected.item(), rel=1e-5)
    assert policy_kl(logits, logits.clone(), mask).item() == pytest.approx(0.0, abs=1e-7)


def test_ref_kl_term_keeps_the_policy_near_the_reference():
    episodes = random_episodes(8, 20, seed=3)

    def train_once(coef):
        torch.manual_seed(0)
        np.random.seed(0)
        model = MiniPokeTransformer(PRESETS["small"])
        ref = copy.deepcopy(model).eval()
        opt = torch.optim.AdamW(model.parameters(), lr=3e-3)
        cfg = PPOConfig(minibatch=40, epochs=4, target_kl=0.0, ref_kl_coef=coef)
        for _ in range(3):
            stats = ppo_update(model, opt, episodes, cfg, "cpu", ref_model=ref)
        return stats

    free, anchored = train_once(0.0), train_once(5.0)
    assert free["ref_kl"] > 0                    # reported without a coefficient too
    assert anchored["ref_kl"] < 0.5 * free["ref_kl"]


def test_ppo_update_reports_when_the_kl_stopped_the_epochs():
    torch.manual_seed(0)
    np.random.seed(0)
    model = MiniPokeTransformer(PRESETS["small"])
    opt = torch.optim.AdamW(model.parameters(), lr=5e-2)
    episodes = random_episodes(8, 20, seed=5)
    stopped = ppo_update(model, opt, episodes, PPOConfig(minibatch=32, epochs=4, target_kl=1e-4,
                                                         kl_stop="mean"), "cpu")
    assert (stopped["epochs"], stopped["kl_stopped"]) == (1, 1)
    assert np.isnan(stopped["ref_kl"])           # no reference given
    full = ppo_update(model, opt, episodes, PPOConfig(minibatch=32, epochs=2, target_kl=0.0), "cpu")
    assert (full["epochs"], full["kl_stopped"]) == (2, 0)


# --------------------------------------------------------------------------
# Options, recipes and resume
# --------------------------------------------------------------------------
def spy_configs(monkeypatch):
    """Records the (PPOConfig, reference model) of every update cmd_ppo makes."""
    configs = []
    real = train.ppo_update

    def spy(model, opt, episodes, cfg, device, ent_coef=None, ref_model=None):
        configs.append((cfg, ref_model))
        return real(model, opt, episodes, cfg, device, ent_coef=ent_coef, ref_model=ref_model)
    monkeypatch.setattr(train, "ppo_update", spy)
    return configs


def test_epochs_and_target_kl_are_options_saved_with_the_run(run_ppo, monkeypatch):
    configs = spy_configs(monkeypatch)
    rows, ck = run_ppo("--size", "small", "--iterations", "1", "--eval-every", "1000",
                       "--epochs", "5", "--target-kl", "0.05")
    assert (configs[0][0].epochs, configs[0][0].target_kl) == (5, 0.05)
    assert (ck["args"]["epochs"], ck["args"]["target_kl"]) == (5, 0.05)
    assert rows[0]["kl_stopped"] in ("0", "1") and rows[0]["ref_kl"] == ""


def test_without_the_new_options_ppo_trains_as_before(run_ppo, monkeypatch):
    configs = spy_configs(monkeypatch)
    _, ck = run_ppo("--size", "medium", "--iterations", "1", "--eval-every", "1000")
    cfg = configs[0][0]
    assert (cfg.epochs, cfg.target_kl, cfg.ref_kl_coef, cfg.lr) == (3, 0.03, 0.0, 1e-4)
    assert ck["args"]["recipe"] is None and configs[0][1] is None


def test_the_v2_mr_recipe_is_minipokev2_1ms_run(run_ppo):
    _, ck = run_ppo("--size", "small", "--iterations", "1", "--eval-every", "1000",
                    "--recipe", "v2-mr")
    # checkpoints/v2-mr/ppo_latest.pt's saved options
    saved = dict(battles_per_iter=192, lr=1e-4, warmup_iters=10, lr_min_ratio=0.1, ent_start=0.01,
                 ent_end=0.003, mirror_share=0.7, pool_share=0.2, heuristic_share=0.1,
                 value_ce_norm="log-bins", vf_coef=0.5, kl_stop="mean", pool_sampling="pfsp",
                 minibatch=64, faint_anneal=0.0, epochs=3, target_kl=0.03)
    # (the fixture passes --minibatch 64: the command line beats the recipe)
    assert {k: ck["args"][k] for k in saved} == saved


def test_recipe_v3_fills_what_the_command_line_leaves(run_ppo, monkeypatch):
    configs = spy_configs(monkeypatch)
    _, ck = run_ppo("--size", "small", "--iterations", "1", "--eval-every", "1000",
                    "--recipe", "v3", "--epochs", "6")
    v3 = train.RECIPES["v3"]
    assert ck["args"]["lr"] == v3["lr"] and ck["args"]["lr_min_ratio"] == v3["lr_min_ratio"]
    assert configs[0][0].epochs == 6 and ck["args"]["recipe"] == "v3"


def test_a_run_saved_before_v3_resumes_with_three_epochs_and_kl_0_03(run_ppo, tmp_path, monkeypatch):
    _, ck = run_ppo("--size", "small", "--iterations", "1", "--eval-every", "1000")
    path = tmp_path / "checkpoints" / "ppo_latest.pt"
    for k in ("epochs", "target_kl", "ref_kl_coef", "ref_ckpt", "pool_extra", "recipe"):
        ck["args"].pop(k)
    torch.save(ck, path)
    configs = spy_configs(monkeypatch)
    args = train.build_parser().parse_args(["ppo", "--device", "cpu", "--resume", str(path),
                                            "--iterations", "2"])
    args.fn(args)
    cfg = configs[0][0]
    assert (cfg.epochs, cfg.target_kl, cfg.ref_kl_coef) == (3, 0.03, 0.0)
    assert load_checkpoint(path)["args"]["epochs"] == 3


def test_ref_ckpt_anchors_the_policy_and_survives_a_resume(run_ppo, tmp_path, monkeypatch):
    ref = MiniPokeTransformer(PRESETS["small"])
    save_checkpoint(tmp_path / "ref.pt", ref)
    configs = spy_configs(monkeypatch)
    rows, ck = run_ppo("--size", "small", "--iterations", "1", "--eval-every", "1000",
                       "--ref-ckpt", str(tmp_path / "ref.pt"), "--ref-kl-coef", "0.1")
    cfg, ref_model = configs[0]
    assert cfg.ref_kl_coef == 0.1 and ref_model is not None
    assert all(not p.requires_grad for p in ref_model.parameters())
    assert float(rows[0]["ref_kl"]) >= 0
    args = train.build_parser().parse_args(["ppo", "--device", "cpu", "--resume",
                                            str(tmp_path / "checkpoints" / "ppo_latest.pt"),
                                            "--iterations", "2"])
    args.fn(args)
    assert configs[-1][1] is not None and configs[-1][0].ref_kl_coef == 0.1


def test_ref_kl_needs_a_reference_with_the_same_encoder(run_ppo, tmp_path):
    with pytest.raises(SystemExit, match="--ref-ckpt"):
        run_ppo("--size", "small", "--iterations", "1", "--ref-kl-coef", "0.1")
    from tests.helpers import make_model
    save_checkpoint(tmp_path / "v2.pt", make_model("small", encoder_version=2))
    with pytest.raises(SystemExit, match="same encoder"):
        run_ppo("--size", "small", "--iterations", "1", "--ref-ckpt", str(tmp_path / "v2.pt"))


# --------------------------------------------------------------------------
# Fixed pool opponents
# --------------------------------------------------------------------------
def test_fixed_opponents_stay_outside_the_cap_and_the_recent_snapshots(tmp_path):
    model = MiniPokeTransformer(PRESETS["small"])
    pool = OpponentPool(tmp_path, cap=4, keep_recent=2)
    pool.add(model, 0)
    fixed = pool.add_fixed(model.state_dict(), model.cfg, "bc.pt")
    for it in range(10, 200, 10):
        pool.add(model, it)
    assert fixed in pool.snapshots and fixed.iteration == -1 and fixed.fixed
    assert len([s for s in pool.snapshots if not s.fixed]) == 4
    assert all(not s.fixed for s in pool.recent(10))
    other = OpponentPool(tmp_path, cap=4)
    other.load_state_dict(pool.state_dict())
    assert other.snapshots == pool.snapshots
    assert set(other.load_weights(fixed)) == set(model.state_dict())


POOL = ("--mirror-share", "0.7", "--pool-share", "0.2", "--heuristic-share", "0.1")


def test_pool_extra_needs_the_learners_architecture(run_ppo, tmp_path):
    save_checkpoint(tmp_path / "bc.pt", MiniPokeTransformer(PRESETS["small"]))
    with pytest.raises(SystemExit, match="architecture"):
        run_ppo("--size", "medium", "--iterations", "1", "--pool-extra", str(tmp_path / "bc.pt"))


def test_pool_extra_joins_the_pool_once_and_is_played(run_ppo, tmp_path):
    bc = MiniPokeTransformer(PRESETS["small"])
    save_checkpoint(tmp_path / "bc.pt", bc)
    _, ck = run_ppo("--size", "small", "--iterations", "1", "--eval-every", "1000", *POOL,
                    "--pool-extra", str(tmp_path / "bc.pt"))
    snaps = ck["pool"]["snapshots"]
    fixed = [s for s in snaps if s["fixed"]]
    assert len(fixed) == 1 and fixed[0]["source"] == str(tmp_path / "bc.pt")
    assert fixed[0]["games"] == 10       # 2 snapshots, 2 slots: both played (FakeRollouts: 10 each)
    assert "fixed1" in ck["elo"]["ratings"]
    args = train.build_parser().parse_args(["ppo", "--device", "cpu", "--resume",
                                            str(tmp_path / "checkpoints" / "ppo_latest.pt"),
                                            "--iterations", "2", "--eval-every", "1000"])
    args.fn(args)
    resumed = load_checkpoint(tmp_path / "checkpoints" / "ppo_latest.pt")
    assert sum(s["fixed"] for s in resumed["pool"]["snapshots"]) == 1
