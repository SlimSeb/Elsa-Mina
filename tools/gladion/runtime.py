"""
Helpers for train.py: logging, the CUDA setup, the local Showdown server and memory
reporting.
"""

from __future__ import annotations

import logging
import os
import shlex
import signal
import socket
import subprocess
import sys
import time
from pathlib import Path
from typing import Optional

import torch

log = logging.getLogger("gladion")


def setup_logging(log_file: Optional[Path] = None) -> None:
    fmt = logging.Formatter("%(asctime)s %(levelname).1s %(message)s", "%m-%d %H:%M:%S")
    root = logging.getLogger("gladion")
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
# CUDA (ROCm's PyTorch also calls its GPU `cuda`)
# --------------------------------------------------------------------------
def configure_cuda(device: torch.device) -> None:
    """TF32 float32 matmuls (tensor cores on Ampere and newer NVIDIA GPUs, inputs rounded to a
    10-bit mantissa; no effect elsewhere) and a line naming the GPU."""
    if device.type != "cuda":
        return
    torch.backends.cuda.matmul.allow_tf32 = True
    torch.backends.cudnn.allow_tf32 = True
    props = torch.cuda.get_device_properties(device)
    build = f"ROCm {torch.version.hip}" if torch.version.hip else f"CUDA {torch.version.cuda}"
    log.info("[cuda] %s, %.1f GiB, PyTorch %s (%s)", props.name, props.total_memory / 2**30,
             torch.__version__, build)


def warn_slow_amp(amp: str, device: torch.device) -> None:
    """bfloat16 runs on the tensor cores from NVIDIA Ampere (compute capability 8.0) on: before,
    it is emulated, and slower than float32."""
    if amp != "bf16" or device.type != "cuda" or torch.version.hip:
        return
    capability = torch.cuda.get_device_capability(device)
    if capability < (8, 0):
        log.warning("[cuda] --amp bf16 on %s (compute capability %d.%d): bfloat16 is emulated before "
                    "8.0, expect it slower than float32", torch.cuda.get_device_name(device), *capability)


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
