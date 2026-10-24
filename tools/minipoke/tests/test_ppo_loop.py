"""cmd_ppo end to end without Showdown: the rollouts and the server are faked, the
loop, the schedules, the update, the checkpoints and the metrics are real."""

import csv
import signal

import pytest
import torch

import train
from checkpoint import load_checkpoint
from ppo import Budget, Schedule
from rollout import IterationResult, PlayResult, wilson_interval
from tests.helpers import random_episodes

STATES_PER_ITER = 4 * 25


class FakeServer:
    def stop(self):
        pass


class FakeRollouts:
    """Plays nothing: every iteration returns 4 random 25-step episodes, every
    evaluation the next scripted win rate (best.pt's re-evaluations: best_results)."""
    eval_results = []
    best_results = []
    weights_evaluated = []

    def __init__(self, learner, device, cfg, server=None, rewards=None):
        self.cfg = cfg
        self.rewards = rewards
        self.rebuilds = 0
        self.players = []
        self.evals = []
        self.seed = 0

    def load_slots(self, states):
        pass

    def play_iteration(self, n_mirror, n_pool, n_heuristic):
        self.seed += 1
        slots = [(2, 10), (8, 10)][:self.cfg.pool_slots] if n_pool else []
        heuristic = (7, 10) if n_heuristic else None
        return IterationResult(random_episodes(4, 25, seed=self.seed), PlayResult(True, 0.01),
                               {"mirror": None, "pool": None, "heuristic": None}, slots,
                               heuristic)

    def evaluate(self, n_battles, opponent="heuristic", snapshot_state=None):
        """Heuristic evaluations return the scripted win rates, pool ones 0.5."""
        self.evals.append((opponent, n_battles))
        scripted = opponent == "heuristic" and FakeRollouts.eval_results
        return (FakeRollouts.eval_results.pop(0) if scripted else 0.5), n_battles

    def evaluate_weights(self, state, n_battles):
        self.evals.append(("best.pt", n_battles))
        FakeRollouts.weights_evaluated.append(state)
        return (FakeRollouts.best_results.pop(0) if FakeRollouts.best_results else 0.5), n_battles


@pytest.fixture
def run_ppo(tmp_path, monkeypatch):
    monkeypatch.chdir(tmp_path)
    monkeypatch.setattr(train, "server_for", lambda args, run_dir: FakeServer())
    monkeypatch.setattr(train, "Rollouts", FakeRollouts)
    monkeypatch.setattr(train, "export_checkpoint", lambda ckpt, out: None)
    monkeypatch.setattr(train, "close_players", lambda players: None)
    monkeypatch.setattr(train, "choose_infer_device", lambda args, model, dev: torch.device("cpu"))
    handlers = {s: signal.getsignal(s) for s in (signal.SIGINT, signal.SIGTERM)}

    def run(*argv):
        args = train.build_parser().parse_args(["ppo", "--device", "cpu", "--minibatch", "64",
                                                "--run", "test", *argv])
        args.fn(args)
        with open(tmp_path / "runs" / "test" / "metrics.csv", newline="") as f:
            rows = list(csv.DictReader(f))
        return rows, load_checkpoint(tmp_path / "checkpoints" / "ppo_latest.pt")

    yield run
    for s, h in handlers.items():
        signal.signal(s, h)
    FakeRollouts.eval_results = []
    FakeRollouts.best_results = []
    FakeRollouts.weights_evaluated = []


def test_states_budget_stops_the_run(run_ppo):
    rows, ck = run_ppo("--size", "small", "--states", str(2 * STATES_PER_ITER + 50),
                       "--eval-every", "1000")
    assert [int(r["iteration"]) for r in rows] == [1, 2, 3]
    assert ck["state"]["total_states"] == 3 * STATES_PER_ITER
    assert ck["args"]["states"] == 2 * STATES_PER_ITER + 50


def test_states_budget_drives_the_schedules(run_ppo):
    budget = 4 * STATES_PER_ITER
    rows, _ = run_ppo("--size", "small", "--states", str(budget), "--lr", "1e-3",
                      "--lr-min-ratio", "0.1", "--ent-start", "0.01", "--ent-end", "0.0",
                      "--eval-every", "1000")
    sched = Schedule(lr=1e-3, lr_min_ratio=0.1, ent_start=0.01, ent_end=0.0)
    for i, r in enumerate(rows):  # progress before iteration i + 1 = states so far / budget
        p = i * STATES_PER_ITER / budget
        assert float(r["lr"]) == pytest.approx(sched.lr_at(i + 1, p), rel=1e-5)
        assert float(r["ent_coef"]) == pytest.approx(sched.ent_at(p), rel=1e-5, abs=1e-9)
    assert len(rows) == 4


def test_best_needs_a_confirmed_win_rate(run_ppo):
    # it 1: candidate 0.9, confirmed 0.55 -> best; it 2: candidate 0.8, confirmed 0.50, and
    # best.pt measured again at 0.53 -> not better; it 3: 0.52 does not beat best.pt's 0.53
    FakeRollouts.eval_results = [0.9, 0.55, 0.8, 0.50, 0.52]
    FakeRollouts.best_results = [0.53]
    rows, ck = run_ppo("--size", "small", "--iterations", "3", "--eval-every", "1",
                       "--eval-battles", "200", "--confirm-battles", "400")
    assert [r["confirm_wr"] for r in rows] == ["0.55", "0.5", ""]
    assert [r["incumbent_wr"] for r in rows] == ["", "0.53", ""]
    assert ck["state"]["best_iteration"] == 1
    assert ck["state"]["best_wr"] == pytest.approx(0.53) and ck["state"]["best_n"] == 400
    assert [float(r["best_wr"]) for r in rows] == [0.55, 0.53, 0.53]


def test_a_lucky_best_is_measured_again_and_replaced(run_ppo, tmp_path):
    # it 1 confirmed at a lucky 0.70; it 2 confirmed at 0.62 would never beat that, but
    # best.pt only wins 0.58 of fresh battles
    FakeRollouts.eval_results = [0.9, 0.70, 0.75, 0.62]
    FakeRollouts.best_results = [0.58]
    rows, ck = run_ppo("--size", "small", "--iterations", "2", "--eval-every", "1",
                       "--confirm-battles", "400")
    assert ck["state"]["best_iteration"] == 2
    assert (ck["state"]["best_wr"], ck["state"]["best_n"]) == (0.62, 0)
    assert load_checkpoint(tmp_path / "checkpoints" / "best.pt")["iteration"] == 2


def test_resume_measures_the_saved_best_pt(run_ppo, tmp_path):
    FakeRollouts.eval_results = [0.6, 0.6, 0.7, 0.65]
    run_ppo("--size", "small", "--iterations", "1", "--eval-every", "1")
    best = load_checkpoint(tmp_path / "checkpoints" / "best.pt")["model"]
    FakeRollouts.best_results = [0.5]
    args = train.build_parser().parse_args(["ppo", "--device", "cpu", "--resume",
                                            str(tmp_path / "checkpoints" / "ppo_latest.pt"),
                                            "--iterations", "2", "--eval-every", "1"])
    args.fn(args)
    seen = FakeRollouts.weights_evaluated[0]
    assert all(torch.equal(seen[k], v) for k, v in best.items())
    assert load_checkpoint(tmp_path / "checkpoints" / "best.pt")["iteration"] == 2


def test_budget_is_whichever_ends_first():
    b = Budget(iterations=100, hours=None, states=1000)
    assert b.progress(10, 0.0, 500) == 0.5
    assert b.progress(60, 0.0, 500) == 0.6
    assert Budget(hours=1.0).progress(5, 1800.0, 10**9) == 0.5
    assert Budget().progress(10**6, 10**6, 10**9) == 0.0


def test_wilson_interval():
    lo, hi = wilson_interval(600, 1000)
    assert lo == pytest.approx(0.5693, abs=1e-4) and hi == pytest.approx(0.6299, abs=1e-4)
    assert wilson_interval(0, 0) == (0.0, 1.0)
    lo, hi = wilson_interval(0, 50)
    assert lo == 0.0 and 0.05 < hi < 0.1


def test_medium_records_the_pool_win_rates_and_resume_keeps_old_runs_on_their_settings(
        run_ppo, tmp_path):
    rows, ck = run_ppo("--size", "medium", "--iterations", "2", "--eval-every", "1000",
                       "--snapshot-every", "1", "--faint-anneal-states", "200")
    assert ck["args"]["pool_sampling"] == "pfsp" and ck["args"]["kl_stop"] == "mean"
    games = sum(s["games"] for s in ck["pool"]["snapshots"])
    assert games == 2 * 20                       # 2 iterations x 2 slots x 10 battles
    assert float(rows[-1]["pool_hardest_wr"]) < 0.5
    assert [float(r["faint_reward"]) for r in rows] == pytest.approx([0.1, 0.05])

    # A checkpoint saved before these options existed resumes on the old behaviour
    path = tmp_path / "checkpoints" / "ppo_latest.pt"
    for k in ("value_ce_norm", "kl_stop", "pool_sampling", "vf_coef"):
        ck["args"].pop(k)
    ck["args"]["vf_coef"] = 0.1
    torch.save(ck, path)
    args = train.build_parser().parse_args(["ppo", "--device", "cpu", "--resume", str(path),
                                            "--iterations", "3"])
    args.fn(args)
    resumed = load_checkpoint(path)
    assert (resumed["args"]["value_ce_norm"], resumed["args"]["kl_stop"],
            resumed["args"]["pool_sampling"], resumed["args"]["vf_coef"]) == (
        "none", "last", "uniform", 0.1)
    assert resumed["state"]["iteration"] == 3


def test_elo_table_follows_training_and_eval_games(run_ppo, tmp_path):
    import json
    from elo import ANCHORS
    FakeRollouts.eval_results = [0.8, 0.8]          # eval and confirmation vs the heuristic
    rows, ck = run_ppo("--size", "medium", "--iterations", "2", "--eval-every", "2",
                       "--eval-pool-battles", "50", "--snapshot-every", "1")
    elo = json.loads((tmp_path / "runs" / "test" / "elo.json").read_text())
    ratings = elo["ratings"]
    assert ratings["heuristic"] == ANCHORS["heuristic"] and ratings["random"] == 0.0
    assert {"learner", "it00000", "it00001"} <= set(ratings)
    assert ratings["learner"] > ANCHORS["heuristic"]       # 70% in training, 80% in eval
    assert float(rows[-1]["elo"]) == pytest.approx(ratings["learner"], abs=0.1)
    assert elo["results"]["heuristic|learner"][1] == 2 * 10 + 200 + 400
    assert elo["results"]["it00001|learner"][1] == 10 + 50   # training slot + eval
    assert ck["elo"]["ratings"] == ratings


def test_winloss_reward_is_undiscounted_and_unshaped(run_ppo, monkeypatch):
    import ppo
    gammas = []
    real = ppo.compute_gae
    monkeypatch.setattr(ppo, "compute_gae", lambda eps, gamma, lam: gammas.append(gamma) or
                        real(eps, gamma, lam))
    rows, ck = run_ppo("--size", "small", "--iterations", "2", "--eval-every", "1000",
                       "--reward", "winloss", "--uniform-kl-coef", "0.01",
                       "--faint-anneal-states", "1000")
    assert gammas == [1.0, 1.0]
    assert [float(r["faint_reward"]) for r in rows] == [0.0, 0.0]
    assert all(float(r["uniform_kl"]) > 0 for r in rows)
    assert ck["args"]["reward"] == "winloss"


def test_aux_coefs_add_aux_heads_and_label_the_rollouts(run_ppo):
    configs = []
    real_init = FakeRollouts.__init__

    def spy(self, learner, device, cfg, server=None, rewards=None):
        configs.append(cfg)
        real_init(self, learner, device, cfg, server, rewards)
    FakeRollouts.__init__ = spy
    try:
        rows, ck = run_ppo("--size", "small", "--iterations", "1", "--eval-every", "1000",
                           "--aux-action-coef", "0.5", "--aux-set-coef", "0.2")
    finally:
        FakeRollouts.__init__ = real_init
    assert ck["cfg"]["aux_heads"] and configs[0].aux_labels
    assert any(k.startswith("aux_") for k in ck["model"])
    assert "aux_action_acc" in rows[0]
