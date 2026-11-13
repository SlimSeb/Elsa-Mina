"""
Showdown checkouts, one per server version, to re-simulate replays (resim.py).

A replay's input log starts with `>version <commit>`, the server's git commit. The battle
replays the same only on that version's simulator: a fix to a move or an item changes the
damage rolls and the PRNG calls that follow. So each version gets its own checkout:

    <root>/repo.git            a blobless bare clone of smogon/pokemon-showdown (commits and
                               trees only: each checkout downloads its own files)
    <root>/<commit[:12]>/      a git worktree at that commit, built with `node build`
    <root>/deps/<lock hash>/   node_modules for each distinct package-lock.json, shared by
                               the worktrees through a symlink (npm ci --omit=dev once per lock)
    <root>/failed.json         versions that could not be built, and why (not retried unless
                               asked: a missing commit or a broken build stays broken)

A worktree is ready once `<root>/<commit[:12]>/.minipoke-built` exists. The cache only grows:
delete a worktree folder (and `git -C repo.git worktree prune`) to rebuild it.
"""

from __future__ import annotations

import hashlib
import json
import logging
import shutil
import subprocess
import time
from pathlib import Path
from typing import Callable, Dict, Optional

REMOTE = "https://github.com/smogon/pokemon-showdown.git"
DEFAULT_ROOT = Path(__file__).resolve().parent / "showdown-versions"
BUILT = ".minipoke-built"
SIM_ENTRY = Path("dist") / "sim" / "battle-stream.js"

log = logging.getLogger("minipoke")


class BuildFailed(Exception):
    """This version has no usable checkout (missing commit, failed install or build)."""


def _run(cmd, cwd: Optional[Path] = None, timeout: float = 1800.0) -> str:
    """Runs a command, returns its output; raises BuildFailed with the tail of it on failure."""
    try:
        out = subprocess.run(cmd, cwd=str(cwd) if cwd else None, capture_output=True, text=True,
                             timeout=timeout)
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise BuildFailed(f"{' '.join(map(str, cmd))}: {exc}") from exc
    if out.returncode != 0:
        tail = (out.stderr or out.stdout).strip().splitlines()[-8:]
        raise BuildFailed(f"{' '.join(map(str, cmd))} exited {out.returncode}: " + " | ".join(tail))
    return out.stdout


def short(commit: str) -> str:
    return commit.strip()[:12]


class ShowdownVersions:
    def __init__(self, root: Path | str = DEFAULT_ROOT, remote: str = REMOTE,
                 runner: Callable = _run):
        self.root = Path(root)
        self.remote = remote
        self.repo = self.root / "repo.git"
        self.run = runner

    # ---- paths and records
    def path(self, commit: str) -> Path:
        return self.root / short(commit)

    def is_built(self, commit: str) -> bool:
        return (self.path(commit) / BUILT).exists()

    def failures(self) -> Dict[str, str]:
        path = self.root / "failed.json"
        return json.loads(path.read_text()) if path.exists() else {}

    def _record_failure(self, commit: str, why: str) -> None:
        failures = self.failures()
        failures[short(commit)] = why
        self.root.mkdir(parents=True, exist_ok=True)
        (self.root / "failed.json").write_text(json.dumps(failures, indent=1))

    def _clear_failure(self, commit: str) -> None:
        failures = self.failures()
        if failures.pop(short(commit), None) is not None:
            (self.root / "failed.json").write_text(json.dumps(failures, indent=1))

    # ---- the clone
    def ensure_repo(self) -> None:
        if (self.repo / "HEAD").exists():
            return
        self.root.mkdir(parents=True, exist_ok=True)
        log.info("[versions] cloning %s (blobless) into %s", self.remote, self.repo)
        self.run(["git", "clone", "--bare", "--filter=blob:none", self.remote, str(self.repo)])

    def _has_commit(self, commit: str) -> bool:
        try:
            self.run(["git", "-C", str(self.repo), "cat-file", "-e", f"{commit}^{{commit}}"], timeout=60)
            return True
        except BuildFailed:
            return False

    def ensure_commit(self, commit: str) -> str:
        """The full hash of `commit`, fetched if the clone does not have it yet."""
        if not self._has_commit(commit):
            log.info("[versions] fetching %s", short(commit))
            try:
                self.run(["git", "-C", str(self.repo), "fetch", "--quiet", "origin",
                          "+refs/heads/*:refs/heads/*"])
            except BuildFailed as exc:
                log.warning("[versions] fetch failed: %s", exc)
            if not self._has_commit(commit) and len(commit) == 40:
                try:   # a commit no branch points to any more (GitHub serves reachable SHAs)
                    self.run(["git", "-C", str(self.repo), "fetch", "--quiet", "origin", commit])
                except BuildFailed:
                    pass
            if not self._has_commit(commit):
                raise BuildFailed(f"commit {short(commit)} is not in {self.remote}")
        return self.run(["git", "-C", str(self.repo), "rev-parse", f"{commit}^{{commit}}"],
                        timeout=60).strip()

    # ---- one version
    def ensure(self, commit: str, retry: bool = False) -> Path:
        """The built checkout of `commit` (cached). Raises BuildFailed, and records why."""
        path = self.path(commit)
        if (path / BUILT).exists():
            return path
        failure = self.failures().get(short(commit))
        if failure and not retry:
            raise BuildFailed(failure)
        try:
            self._build(commit, path)
        except BuildFailed as exc:
            self._record_failure(commit, str(exc))
            raise
        self._clear_failure(commit)
        return path

    def _build(self, commit: str, path: Path) -> None:
        t0 = time.time()
        self.ensure_repo()
        full = self.ensure_commit(commit)
        if not (path / "package.json").exists():
            if path.exists():
                shutil.rmtree(path)
            self.run(["git", "-C", str(self.repo), "worktree", "prune"], timeout=60)
            self.run(["git", "-C", str(self.repo), "worktree", "add", "--detach", "--force",
                      str(path), full])
        deps = self._deps(path)
        link = path / "node_modules"
        if link.is_symlink() or not link.exists():
            if link.is_symlink():
                link.unlink()
            link.symlink_to(deps / "node_modules", target_is_directory=True)
        log.info("[versions] building %s", short(commit))
        self.run(["node", "build"], cwd=path)
        if not (path / SIM_ENTRY).exists():
            raise BuildFailed(f"node build left no {SIM_ENTRY}")
        (path / BUILT).write_text(json.dumps({"commit": full, "deps": deps.name,
                                              "built": time.strftime("%Y-%m-%d %H:%M:%S"),
                                              "seconds": round(time.time() - t0)}))
        log.info("[versions] %s ready (%.0f s)", short(commit), time.time() - t0)

    def _deps(self, path: Path) -> Path:
        """node_modules for this checkout's package-lock.json, installed once per lock file."""
        lock = path / "package-lock.json"
        if not lock.exists():
            raise BuildFailed("no package-lock.json")
        digest = hashlib.sha256(lock.read_bytes() + (path / "package.json").read_bytes()).hexdigest()[:12]
        deps = self.root / "deps" / digest
        if not (deps / "node_modules" / ".package-lock.json").exists():
            deps.mkdir(parents=True, exist_ok=True)
            for name in ("package.json", "package-lock.json"):
                shutil.copyfile(path / name, deps / name)
            log.info("[versions] npm ci for lock %s (%s)", digest, path.name)
            self.run(["npm", "ci", "--omit=dev", "--no-audit", "--no-fund"], cwd=deps)
        return deps
