"""
League-lite: a pool of frozen past policies to play against, so PPO does not
only chase its own mirror (which can cycle or forget how to beat older styles).
"""

from __future__ import annotations

import random
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import List, Optional, Tuple

import torch


@dataclass
class Snapshot:
    iteration: int
    file: str


class OpponentPool:
    """Snapshots on disk (weights only), at most `cap` of them.

    Eviction keeps a spread of ages: the `keep_recent` newest and the oldest are
    never removed; otherwise the snapshot whose removal leaves the smallest gap
    in iterations goes. Ages end up roughly evenly spaced over the run.
    """

    def __init__(self, directory: Path | str, cap: int = 20, keep_recent: int = 5):
        self.dir = Path(directory)
        self.cap = cap
        self.keep_recent = keep_recent
        self.snapshots: List[Snapshot] = []

    def __len__(self) -> int:
        return len(self.snapshots)

    def add(self, model: torch.nn.Module, iteration: int) -> Snapshot:
        self.dir.mkdir(parents=True, exist_ok=True)
        snap = Snapshot(iteration, f"snap_{iteration:05d}.pt")
        state = {k: v.detach().cpu() for k, v in model.state_dict().items()}
        torch.save({"model": state, "cfg": model.cfg.to_dict(), "iteration": iteration},
                   self.dir / snap.file)
        self.snapshots = [s for s in self.snapshots if s.iteration != iteration] + [snap]
        self.snapshots.sort(key=lambda s: s.iteration)
        while len(self.snapshots) > self.cap:
            self._evict()
        return snap

    def _evict(self) -> None:
        its = [s.iteration for s in self.snapshots]
        candidates = range(1, len(its) - self.keep_recent)
        if not candidates:
            victim = 0
        else:
            victim = min(candidates, key=lambda i: (its[i + 1] - its[i - 1], its[i]))
        snap = self.snapshots.pop(victim)
        (self.dir / snap.file).unlink(missing_ok=True)

    def sample(self, k: int, rng: random.Random) -> List[Snapshot]:
        """k distinct snapshots, uniformly (fewer if the pool is smaller)."""
        return rng.sample(self.snapshots, min(k, len(self.snapshots)))

    def recent(self, n: int) -> List[Snapshot]:
        return list(reversed(self.snapshots[-n:]))

    def load_weights(self, snap: Snapshot) -> dict:
        return torch.load(self.dir / snap.file, map_location="cpu", weights_only=False)["model"]

    def state_dict(self) -> dict:
        return {"snapshots": [asdict(s) for s in self.snapshots]}

    def load_state_dict(self, state: Optional[dict]) -> None:
        snaps = [Snapshot(**s) for s in (state or {}).get("snapshots", [])]
        self.snapshots = [s for s in snaps if (self.dir / s.file).exists()]


def split_battles(n: int, mirror: float, pool: float, heuristic: float) -> Tuple[int, int, int]:
    """Battles per opponent type. Shares are renormalised; every type with a
    positive share gets at least one battle; the counts add up to n."""
    shares = [max(mirror, 0.0), max(pool, 0.0), max(heuristic, 0.0)]
    total = sum(shares) or 1.0
    counts = [int(n * s / total) for s in shares]
    for i, s in enumerate(shares):
        if s > 0 and counts[i] == 0 and n >= sum(1 for x in shares if x > 0):
            counts[i] = 1
    # remainder to the largest share
    big = max(range(3), key=lambda i: shares[i])
    counts[big] += n - sum(counts)
    return counts[0], counts[1], counts[2]


def split_evenly(n: int, k: int) -> List[int]:
    return [n // k + (1 if i < n % k else 0) for i in range(k)]
