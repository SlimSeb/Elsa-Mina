"""
Helpers for long (12-48 h) runs: logging, MPS fallback detection, the local
Showdown server, clean stops, metrics CSV, checkpoint retention, inference
device benchmark and memory reporting.
"""

from __future__ import annotations

import copy
import csv
import logging
import math
import os
import re
import shlex
import signal
import socket
import subprocess
import sys
import threading
import time
import warnings
from pathlib import Path
from typing import Dict, Iterable, List, Optional

import torch

log = logging.getLogger("minipoke")


def setup_logging(log_file: Optional[Path] = None) -> None:
    fmt = logging.Formatter("%(asctime)s %(levelname).1s %(message)s", "%m-%d %H:%M:%S")
    root = logging.getLogger("minipoke")
    root.setLevel(logging.INFO)
    root.propagate = False
    if not root.handlers:
        h = logging.StreamHandler(sys.stdout)
        h.setFormatter(fmt)
        root.addHandler(h)
    if log_file is not None:
        log_file.parent.mkdir(parents=True, exist_ok=True)
        fh = logging.FileHandler(log_file, encoding="utf-8")
        fh.setFormatter(fmt)
        root.addHandler(fh)


# --------------------------------------------------------------------------
# MPS: PYTORCH_ENABLE_MPS_FALLBACK=1 keeps unsupported ops running (on the CPU,
# slowly) instead of crashing. Make every fallback loud.
# --------------------------------------------------------------------------
_MPS_FALLBACK = re.compile(r"not currently supported on the MPS backend and will fall back")
mps_fallback_ops: List[str] = []


def install_mps_fallback_warning() -> None:
    previous = warnings.showwarning

    def show(message, category, filename, lineno, file=None, line=None):
        text = str(message)
        if _MPS_FALLBACK.search(text):
            op = re.search(r"'([^']+)'", text)
            name = op.group(1) if op else text
            if name not in mps_fallback_ops:
                mps_fallback_ops.append(name)
                log.warning("[mps-fallback] %s runs on the CPU (PYTORCH_ENABLE_MPS_FALLBACK); "
                            "this slows training down", name)
            return
        previous(message, category, filename, lineno, file, line)

    warnings.showwarning = show
    warnings.filterwarnings("always", message=".*MPS backend.*fall back.*")


def mps_self_check(model: torch.nn.Module, device: torch.device, sample: Dict[str, torch.Tensor]) -> None:
    """One forward + backward on the training device, to surface any CPU fallback
    at startup rather than hours into a run."""
    if device.type != "mps":
        return
    m = copy.deepcopy(model).to(device).train()
    before = len(mps_fallback_ops)
    logits, values, value_logits = m.forward_train({k: v.to(device) for k, v in sample.items()})
    loss = torch.log_softmax(logits, -1).mean() + values.mean()
    if value_logits is not None:
        loss = loss + torch.log_softmax(value_logits, -1).mean()
    loss.backward()
    torch.mps.synchronize()
    if len(mps_fallback_ops) == before:
        log.info("[mps] forward/backward self-check: no CPU fallback")


# --------------------------------------------------------------------------
# Local Showdown server
# --------------------------------------------------------------------------
class ShowdownServer:
    """Checks the server's port and, given a command, (re)starts it."""

    def __init__(self, cmd: Optional[str], cwd: Optional[str], host: str = "localhost",
                 port: int = 8000, log_path: Optional[Path] = None):
        self.cmd, self.cwd, self.host, self.port = cmd, cwd, host, port
        self.log_path = log_path
        self.proc: Optional[subprocess.Popen] = None
        self.restarts = 0

    def is_up(self, timeout: float = 2.0) -> bool:
        try:
            with socket.create_connection((self.host, self.port), timeout=timeout):
                return True
        except OSError:
            return False

    def ensure_up(self, wait: float = 120.0) -> bool:
        if self.is_up():
            return True
        if not self.cmd:
            log.error("Showdown is not answering on %s:%d (pass --showdown-cmd to let "
                      "training start it)", self.host, self.port)
            return False
        self.stop()
        log.warning("starting Showdown: %s (cwd %s)", self.cmd, self.cwd or ".")
        out = open(self.log_path, "ab") if self.log_path else subprocess.DEVNULL
        # Own process group, so stop() also ends Showdown's simulator subprocesses
        group = ({"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == "nt"
                 else {"start_new_session": True})
        self.proc = subprocess.Popen(shlex.split(self.cmd, posix=os.name != "nt"), cwd=self.cwd,
                                     stdout=out, stderr=subprocess.STDOUT, **group)
        self.restarts += 1
        deadline = time.monotonic() + wait
        while time.monotonic() < deadline:
            if self.proc.poll() is not None:
                log.error("Showdown exited with code %s", self.proc.returncode)
                return False
            if self.is_up():
                time.sleep(2.0)  # let it finish booting its simulator processes
                return True
            time.sleep(1.0)
        log.error("Showdown did not open port %d within %.0fs", self.port, wait)
        return False

    def stop(self) -> None:
        """Stops a server this object started (with its whole process tree)."""
        if self.proc is not None and self.proc.poll() is None:
            if os.name == "nt":
                subprocess.run(["taskkill", "/T", "/F", "/PID", str(self.proc.pid)],
                               capture_output=True)
            else:
                try:
                    os.killpg(self.proc.pid, signal.SIGTERM)
                except ProcessLookupError:
                    pass
            try:
                self.proc.wait(10)
            except subprocess.TimeoutExpired:
                if os.name != "nt":
                    os.killpg(self.proc.pid, signal.SIGKILL)
        self.proc = None


# --------------------------------------------------------------------------
# Clean stops: SIGINT / SIGTERM / a STOP file finish the current iteration
# --------------------------------------------------------------------------
class StopController:
    def __init__(self, stop_file: Optional[Path] = None):
        self.stop_file = stop_file
        self.reason: Optional[str] = None
        self._count = 0

    def install(self) -> "StopController":
        if threading.current_thread() is threading.main_thread():
            for sig in (signal.SIGINT, getattr(signal, "SIGTERM", None)):
                if sig is not None:
                    signal.signal(sig, self._handle)
        return self

    def _handle(self, signum, frame) -> None:
        self._count += 1
        if self._count >= 2:
            log.warning("second signal: exiting now without saving")
            raise KeyboardInterrupt
        self.reason = signal.Signals(signum).name
        log.warning("%s received: finishing the current iteration, then saving "
                    "(send it again to quit immediately)", self.reason)

    def request(self, reason: str) -> None:
        self.reason = self.reason or reason

    @property
    def stop(self) -> bool:
        if self.reason is None and self.stop_file is not None and self.stop_file.exists():
            self.reason = f"stop file {self.stop_file}"
            log.warning("%s found: finishing the current iteration, then saving", self.stop_file)
        return self.reason is not None


# --------------------------------------------------------------------------
# Metrics: one CSV row per iteration
# --------------------------------------------------------------------------
class MetricsLogger:
    def __init__(self, path: Path, columns: List[str]):
        self.path = path
        self.columns = columns
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists():
            with open(path, newline="") as f:
                header = next(csv.reader(f), None)
            if header != columns:  # older layout: keep it, start a new file
                path.rename(path.with_name(f"{path.stem}.{int(time.time())}.csv"))
        if not path.exists():
            with open(path, "w", newline="") as f:
                csv.writer(f).writerow(columns)

    def append(self, row: Dict[str, object]) -> None:
        def fmt(v):
            if v is None or (isinstance(v, float) and math.isnan(v)):
                return ""
            return f"{v:.6g}" if isinstance(v, float) else v
        with open(self.path, "a", newline="") as f:
            csv.writer(f).writerow([fmt(row.get(c)) for c in self.columns])


# --------------------------------------------------------------------------
# Checkpoint retention: the last `keep_last`, plus every `keep_every`-th
# --------------------------------------------------------------------------
def prune_checkpoints(directory: Path, prefix: str = "ppo_", keep_last: int = 5,
                      keep_every: int = 50) -> List[Path]:
    pattern = re.compile(rf"^{re.escape(prefix)}(\d+)\.pt$")
    numbered = sorted((int(m.group(1)), p) for p in directory.glob(f"{prefix}*.pt")
                      if (m := pattern.match(p.name)))
    keep = {it for it, _ in numbered[-keep_last:]} | {it for it, _ in numbered
                                                       if keep_every and it % keep_every == 0}
    removed = []
    for it, path in numbered:
        if it not in keep:
            path.unlink(missing_ok=True)
            removed.append(path)
    return removed


# --------------------------------------------------------------------------
# Rollout inference device: CPU vs the accelerator, at rollout batch sizes
# --------------------------------------------------------------------------
def _sync(device: torch.device) -> None:
    if device.type == "cuda":
        torch.cuda.synchronize()
    elif device.type == "mps":
        torch.mps.synchronize()


@torch.no_grad()
def benchmark_inference(model: torch.nn.Module, make_batch, devices: Iterable[torch.device],
                        batch_sizes: Iterable[int], repeats: int = 20) -> Dict[str, Dict[int, float]]:
    """Median ms per forward for each device and batch size (including the
    host->device copy, as in rollouts)."""
    results: Dict[str, Dict[int, float]] = {}
    for device in devices:
        m = copy.deepcopy(model).to(device).eval()
        per_size = {}
        for bs in batch_sizes:
            batch = make_batch(bs)
            for _ in range(3):
                m({k: v.to(device) for k, v in batch.items()})
            _sync(device)
            times = []
            for _ in range(repeats):
                t0 = time.perf_counter()
                logits, _ = m({k: v.to(device) for k, v in batch.items()})
                logits.cpu()
                times.append(time.perf_counter() - t0)
            per_size[bs] = sorted(times)[len(times) // 2] * 1e3
        results[str(device)] = per_size
    return results


def pick_infer_device(model, make_batch, train_device: torch.device, concurrency: int) -> torch.device:
    devices = [torch.device("cpu")]
    if train_device.type in ("mps", "cuda"):
        devices.append(train_device)
    if len(devices) == 1:
        return devices[0]
    sizes = sorted({max(1, concurrency // 4), max(1, concurrency // 2), concurrency, 2 * concurrency})
    res = benchmark_inference(model, make_batch, devices, sizes)
    for dev, per in res.items():
        log.info("[bench] %s: %s", dev, "  ".join(f"b{bs} {ms:.2f}ms" for bs, ms in per.items()))
    # Throughput at the batch sizes rollouts actually see
    score = {dev: sum(ms / bs for bs, ms in per.items()) for dev, per in res.items()}
    best = min(score, key=score.get)
    log.info("[bench] rollout inference on %s", best)
    return torch.device(best)


# --------------------------------------------------------------------------
# Memory
# --------------------------------------------------------------------------
def rss_mb() -> Optional[float]:
    try:
        import psutil
        return psutil.Process().memory_info().rss / 2**20
    except ImportError:
        return None


def peak_rss_mb() -> Optional[float]:
    try:
        import psutil
        info = psutil.Process().memory_info()
        if hasattr(info, "peak_wset"):  # Windows
            return info.peak_wset / 2**20
    except ImportError:
        pass
    try:
        import resource
        peak = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
        return peak / 2**20 if sys.platform == "darwin" else peak / 2**10
    except ImportError:
        return rss_mb()
