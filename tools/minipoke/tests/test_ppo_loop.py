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
    evaluation the next scripted win rate."""
    eval_results = []

    def __init__(self, learner, device, cfg, server=None):
        self.cfg = cfg
        self.rebuilds = 0
        self.players = []
        self.evals = []
        self.seed = 0

    def load_slots(self, states):
        pass

    def play_iteration(self, n_mirror, n_pool, n_heuristic):
        self.seed += 1
        return IterationResult(random_episodes(4, 25, seed=self.seed), PlayResult(True, 0.01),
                               {"mirror": None, "pool": None, "heuristic": None})

    def evaluate(self, n_battles, opponent="heuristic", snapshot_state=None):
        self.evals.append((opponent, n_battles))
        wr = FakeRollouts.eval_results.pop(0) if FakeRollouts.eval_results else 0.5
        return wr, n_battles


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
    # it 1: candidate 0.9, confirmed 0.55 -> best; it 2: candidate 0.8, confirmed 0.50 -> not
    # better; it 3: 0.52 does not beat the confirmed 0.55 -> no confirmation at all
    FakeRollouts.eval_results = [0.9, 0.55, 0.8, 0.50, 0.52]
    rows, ck = run_ppo("--size", "small", "--iterations", "3", "--eval-every", "1",
                       "--eval-battles", "200", "--confirm-battles", "400")
    assert [r["confirm_wr"] for r in rows] == ["0.55", "0.5", ""]
    assert ck["state"]["best_wr"] == 0.55 and ck["state"]["best_iteration"] == 1
    assert [float(r["best_wr"]) for r in rows] == [0.55, 0.55, 0.55]


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
