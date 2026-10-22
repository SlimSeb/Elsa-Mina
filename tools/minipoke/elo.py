"""
Elo league table: one rating per player (the learner, every pool snapshot, and
the scripted players SimpleHeuristicsPlayer, MaxBasePowerPlayer, RandomPlayer).

The scripted players are anchors: their ratings are fixed (random = 0), so ratings
mean the same thing in every run and can be compared across runs. During PPO the
table is updated online after every iteration, from all training and evaluation
games (mirror games, learner vs itself, carry no information and are skipped):

    delta = k * (score - expected),  expected = 1 / (1 + 10^((Rb - Ra) / 400))

once per game (a batch of n games: n updates at the batch's average score), applied
to both players (anchors excepted). The learner starts at the heuristic's rating
(runs start from BC or distillation), a new snapshot at the learner's rating. `train.py elo` instead plays a round-robin between fixed players and fits
the ratings by maximum likelihood (Bradley-Terry, anchors fixed).

A tie counts as half a win. Training seats only count wins, so a training tie is a
loss there (ties are rare in random battles).
"""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple

# Fitted with `train.py elo --scripted heuristic maxpower random --anchors random=0`:
# 2,500 gen9randombattle battles per pair on a local Showdown (commit 9b136a8, Oct 2026,
# poke-env 0.16.1): heuristic beat maxpower 89.0%, random 99.1%; maxpower beat random
# 91.2% -> 777 and 410. Showdown's sets change over time: refit before comparing old runs.
ANCHORS = {"random": 0.0, "maxpower": 410.0, "heuristic": 780.0}
LEARNER = "learner"


def expected(ra: float, rb: float) -> float:
    return 1.0 / (1.0 + 10 ** ((rb - ra) / 400.0))


def snapshot_name(iteration: int) -> str:
    return f"it{iteration:05d}"


def parse_anchors(text: Optional[str]) -> Dict[str, float]:
    """"random=0,maxpower=350" -> {"random": 0.0, "maxpower": 350.0}; None -> ANCHORS."""
    if text is None:
        return dict(ANCHORS)
    out = {}
    for part in filter(None, (p.strip() for p in text.split(","))):
        name, value = part.split("=")
        out[name.strip()] = float(value)
    return out


class EloTable:
    def __init__(self, anchors: Optional[Dict[str, float]] = None, k: float = 16.0):
        self.anchors = dict(ANCHORS if anchors is None else anchors)
        self.k = k
        self.ratings: Dict[str, float] = dict(self.anchors)
        self.games: Dict[str, int] = {name: 0 for name in self.anchors}
        # "a|b" -> [score of a, games]
        self.results: Dict[str, List[float]] = {}

    def rating(self, name: str) -> float:
        return self.ratings.get(name, 0.0)

    def add_player(self, name: str, rating: Optional[float] = None) -> None:
        if name not in self.ratings:
            self.ratings[name] = self.rating(LEARNER) if rating is None else rating
            self.games[name] = 0

    def record(self, a: str, b: str, score_a: float, games: int) -> None:
        """`a` scored `score_a` (wins + ties / 2) over `games` games against `b`: as
        many one-game Elo updates, each scoring the batch's average. (One update of
        k * (score - games * expected) would overshoot by far for 200-game batches.)"""
        if games <= 0 or a == b:
            return
        for name in (a, b):
            self.add_player(name)
        ra, rb, p = self.ratings[a], self.ratings[b], score_a / games
        for _ in range(games):
            delta = self.k * (p - expected(ra, rb))
            ra += 0.0 if a in self.anchors else delta
            rb -= 0.0 if b in self.anchors else delta
        self.ratings[a], self.ratings[b] = ra, rb
        self.games[a] += games
        self.games[b] += games
        key, flip = (f"{a}|{b}", False) if a < b else (f"{b}|{a}", True)
        acc = self.results.setdefault(key, [0.0, 0])
        acc[0] += games - score_a if flip else score_a
        acc[1] += games

    def table(self) -> List[Tuple[str, float, int]]:
        return sorted(((n, r, self.games.get(n, 0)) for n, r in self.ratings.items()),
                      key=lambda t: -t[1])

    def state_dict(self) -> dict:
        return {"anchors": self.anchors, "k": self.k, "ratings": self.ratings,
                "games": self.games, "results": self.results}

    def load_state_dict(self, state: Optional[dict]) -> None:
        if not state:
            return
        self.ratings = {**dict(state["ratings"]), **self.anchors}
        self.games = dict(state["games"])
        self.results = {k: list(v) for k, v in state["results"].items()}

    def save(self, path: Path | str) -> None:
        path = Path(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        tmp = path.with_name(path.name + ".tmp")
        tmp.write_text(json.dumps({"table": [{"player": n, "rating": round(r, 1), "games": g}
                                             for n, r, g in self.table()],
                                   **self.state_dict()}, indent=1))
        tmp.replace(path)


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
