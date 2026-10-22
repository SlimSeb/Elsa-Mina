import signal

import pytest

from runtime import MetricsLogger, StopController, prune_checkpoints


def test_first_sigint_requests_a_clean_stop_and_the_second_quits():
    previous = signal.getsignal(signal.SIGINT)
    try:
        stop = StopController().install()
        assert not stop.stop
        signal.raise_signal(signal.SIGINT)
        assert stop.stop and stop.reason == "SIGINT"
        with pytest.raises(KeyboardInterrupt):
            signal.raise_signal(signal.SIGINT)
    finally:
        signal.signal(signal.SIGINT, previous)


def test_stop_file(tmp_path):
    stop = StopController(tmp_path / "STOP")
    assert not stop.stop
    (tmp_path / "STOP").touch()
    assert stop.stop


def test_retention_keeps_the_last_five_and_every_fiftieth(tmp_path):
    for it in range(10, 310, 10):
        (tmp_path / f"ppo_{it:05d}.pt").touch()
    (tmp_path / "ppo_latest.pt").touch()
    (tmp_path / "best.pt").touch()
    prune_checkpoints(tmp_path, keep_last=5, keep_every=50)
    left = sorted(p.name for p in tmp_path.iterdir())
    assert left == sorted(["best.pt", "ppo_latest.pt"] +
                          [f"ppo_{i:05d}.pt" for i in (50, 100, 150, 200, 250, 260, 270, 280, 290, 300)])


def test_metrics_csv_appends_across_resumes(tmp_path):
    path = tmp_path / "metrics.csv"
    MetricsLogger(path, ["iteration", "lr"]).append({"iteration": 1, "lr": 2e-4})
    MetricsLogger(path, ["iteration", "lr"]).append({"iteration": 2, "lr": float("nan")})
    assert path.read_text().splitlines() == ["iteration,lr", "1,0.0002", "2,"]
