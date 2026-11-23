"""
Ratings for `train.py elo`: a round-robin between checkpoints, released models and the
scripted players (SimpleHeuristicsPlayer, MaxBasePowerPlayer, RandomPlayer), fitted by
maximum likelihood (Bradley-Terry) on the Elo scale:

    expected = 1 / (1 + 10^((Rb - Ra) / 400))

The scripted players are anchors: their ratings are fixed (random = 0), so ratings mean
the same thing in every round-robin and can be compared across them. A tie counts as half
a win.
"""

from __future__ import annotations

import math
from typing import Dict, Iterable, List, Optional, Tuple

# Fitted with `train.py elo --scripted heuristic maxpower random --anchors random=0`:
# 2,500 gen9randombattle battles per pair on a local Showdown (commit 9b136a8, Oct 2026,
# poke-env 0.16.1): heuristic beat maxpower 89.0%, random 99.1%; maxpower beat random
# 91.2% -> 777 and 410. Showdown's sets change over time: refit before comparing old runs.
ANCHORS = {"random": 0.0, "maxpower": 410.0, "heuristic": 780.0}


def expected(ra: float, rb: float) -> float:
    return 1.0 / (1.0 + 10 ** ((rb - ra) / 400.0))


def parse_anchors(text: Optional[str]) -> Dict[str, float]:
    """"random=0,maxpower=350" -> {"random": 0.0, "maxpower": 350.0}; None -> ANCHORS."""
    if text is None:
        return dict(ANCHORS)
    out = {}
    for part in filter(None, (p.strip() for p in text.split(","))):
        name, value = part.split("=")
        out[name.strip()] = float(value)
    return out


def fit_ratings(results: Dict[Tuple[str, str], Tuple[float, int]], anchors: Dict[str, float],
                iterations: int = 2000, prior_games: float = 1.0) -> Dict[str, float]:
    """Bradley-Terry maximum likelihood (Hunter's MM updates) on the Elo scale, the
    players named in `anchors` held at their rating. Each pair gets `prior_games`
    virtual games split evenly, so a player who never won keeps a finite rating.
    Without any anchor, the ratings are centred on 0."""
    players = sorted({p for pair in results for p in pair})
    score: Dict[str, float] = {p: 0.0 for p in players}
    games: Dict[Tuple[str, str], float] = {}
    for (a, b), (s, n) in results.items():
        if n <= 0:
            continue
        score[a] += s + prior_games / 2
        score[b] += n - s + prior_games / 2
        games[(a, b)] = games.get((a, b), 0.0) + n + prior_games
    gamma = {p: 10 ** (anchors.get(p, 0.0) / 400.0) for p in players}
    for _ in range(iterations):
        new = {}
        for p in players:
            if p in anchors:
                new[p] = gamma[p]
                continue
            denom = sum(n / (gamma[a] + gamma[b]) for (a, b), n in games.items() if p in (a, b))
            new[p] = score[p] / denom if denom > 0 else gamma[p]
        if not any(p in anchors for p in players):  # no anchor: fix the scale's centre
            mean = sum(math.log10(g) for g in new.values()) / len(new)
            new = {p: g / 10 ** mean for p, g in new.items()}
        gamma = new
    return {p: 400.0 * math.log10(g) for p, g in gamma.items()}


def round_robin_pairs(names: Iterable[str]) -> List[Tuple[str, str]]:
    names = list(names)
    return [(a, b) for i, a in enumerate(names) for b in names[i + 1:]]
