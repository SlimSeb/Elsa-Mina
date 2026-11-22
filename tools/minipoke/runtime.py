"""
Helpers for train.py: logging, MPS fallback detection, the local Showdown server and
memory reporting.
"""

from __future__ import annotations

import copy
import logging
import os
import re
import shlex
import signal
import socket
import subprocess
import sys
import time
import warnings
from pathlib import Path
from typing import Dict, List, Optional

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
