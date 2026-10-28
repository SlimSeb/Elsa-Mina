"""
Paired evaluation: every random battle team pair is played twice, the second time
with the teams swapped, so the luck of the teams cancels out of a comparison.

In a random battle, the teams decide more games than the players do. Two models
compared on independent random battles need about 1500 games each for a 95% interval
of +-2.5 points. With paired teams, a pair of games scores 0, 0.5 or 1 for A, and the
interval comes from the spread of those pair scores: the team luck both games share
is gone from it.

Showdown's random battle format generates the teams itself, so paired games are
challenged as `gen9anythinggoes` with custom rules that make it a random battle: no
team preview, no legality checks, no Endless Battle Clause, plus Species Clause,
Sleep Clause Mod and Illusion Level Mod (Anything Goes already shows HP in percent).
Not `gen9customgame`: it is a debug format, where Showdown reports exact HP to both
sides. The teams come from Showdown's own gen 9 random battle generator, seeded
(`generate_teams`, through Node in the Showdown clone). The players see what they see
in a random battle: their own team from the request, the opponent's Pokémon as they
come in, at percent HP.

poke-env sends challenges with `Player._format` and creates a battle only when the
room's format equals `Player.format`: `paired_player` makes the first the format with
its custom rules and the second the bare `gen9anythinggoes`. Battles start in challenge
order and each side's teambuilder yields its teams in that order, so challenge k plays
team pair k on both sides; `pair_scores` checks it from each battle's own team.
"""

from __future__ import annotations

import json
import math
import subprocess
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

from poke_env.teambuilder import Teambuilder

BASE_FORMAT = "gen9anythinggoes"
RULES = ["!Team Preview", "!Obtainable", "!Endless Battle Clause", "Species Clause", "Sleep Clause Mod",
         "Illusion Level Mod"]
PAIRED_FORMAT = f"{BASE_FORMAT}@@@{','.join(RULES)}"

_GENERATE_JS = """
const {Teams} = require('./dist/sim');
const [n, seed] = [Number(process.argv[1]), Number(process.argv[2])];
const teams = [];
for (let i = 0; i < n; i++) {
  const s = 'sodium,' + (seed * 1000003 + i).toString(16).padStart(32, '0');
  teams.push(Teams.pack(Teams.generate('gen9randombattle', {seed: s})));
}
console.log(JSON.stringify(teams));
"""


def generate_teams(n: int, seed: int, showdown_cwd: str | Path) -> List[str]:
    """`n` packed gen 9 random battle teams from Showdown's generator, the same for the
    same seed (and Showdown version)."""
    out = subprocess.run(["node", "-e", _GENERATE_JS, str(n), str(seed)], cwd=str(showdown_cwd),
                         capture_output=True, text=True, check=True)
    return json.loads(out.stdout)


def team_names(packed: str) -> frozenset:
    """The Pokémon names of a packed team (the idents a battle shows)."""
    return frozenset(member.split("|")[0] for member in packed.split("]"))


class TeamSequence(Teambuilder):
    """Yields the given teams in order, one per challenge sent or accepted."""

    def __init__(self, teams: Sequence[str]):
        self.teams = list(teams)
        self.given = 0

    def yield_team(self) -> str:
        team = self.teams[self.given % len(self.teams)]
        self.given += 1
        return team


@dataclass
class TeamPairs:
    """`n` team pairs (T_k, U_k). The challenger plays T_1, U_1, T_2, U_2..., the other
    side U_1, T_1, U_2, T_2...: games 2k-1 and 2k of a match are pair k, swapped."""
    first: List[str]
    second: List[str]

    @classmethod
    def generate(cls, n: int, seed: int, showdown_cwd: str | Path) -> "TeamPairs":
        teams = generate_teams(2 * n, seed, showdown_cwd)
        return cls(teams[:n], teams[n:])

    def __len__(self) -> int:
        return len(self.first)

    def challenger(self) -> TeamSequence:
        return TeamSequence([t for pair in zip(self.first, self.second) for t in pair])

    def accepter(self) -> TeamSequence:
        return TeamSequence([t for pair in zip(self.second, self.first) for t in pair])

    def locate(self, names: frozenset) -> Optional[Tuple[int, int]]:
        """(pair k, 0 for T_k / 1 for U_k) of the team with these names."""
        if not hasattr(self, "_index"):
            self._index: Dict[frozenset, Tuple[int, int]] = {}
            for k, (t, u) in enumerate(zip(self.first, self.second)):
                self._index[team_names(t)] = (k, 0)
                self._index[team_names(u)] = (k, 1)
        return self._index.get(names)


_PAIRED_CLASSES: Dict[type, type] = {}


def paired_player(cls):
    """`cls` playing paired games: challenges in PAIRED_FORMAT, battles of BASE_FORMAT."""
    if getattr(cls, "_paired", False):
        return cls
    if cls in _PAIRED_CLASSES:
        return _PAIRED_CLASSES[cls]

    class Paired(cls):
        _paired = True

        @property
        def format(self) -> str:
            return self._format.split("@@@")[0]

    Paired.__name__ = f"Paired{cls.__name__}"
    Paired.__qualname__ = Paired.__name__
    _PAIRED_CLASSES[cls] = Paired
    return Paired


def make_paired(player, teams: Teambuilder) -> None:
    """Turns a player that has not played yet into one that plays paired games with
    `teams` (its class becomes `paired_player`'s subclass of it)."""
    player.__class__ = paired_player(type(player))
    player._format = PAIRED_FORMAT
    player.update_team(teams)


def battle_score(battle) -> float:
    """1 for a win, 0.5 for a tie (Showdown's turn 1000 auto-tie), 0 for a loss."""
    if battle.won:
        return 1.0
    return 0.0 if battle.lost else 0.5


@dataclass
class PairedResult:
    score: float               # mean of the pair scores (A's view)
    low: float                 # 95% interval of the mean
    high: float
    pairs: int                 # complete pairs (both games finished)
    games: int                 # finished games, incomplete pairs included
    ties: int
    misplaced: int             # games whose own team is not the expected one
    unpaired_halfwidth: float  # the interval's half width for as many independent games
    scores: List[float] = field(default_factory=list, repr=False)  # each complete pair's

    def describe(self) -> str:
        gain = (self.unpaired_halfwidth / ((self.high - self.low) / 2)) ** 2 \
            if self.high > self.low else float("nan")
        return (f"{self.score:.1%} over {self.pairs} team pairs ({2 * self.pairs} games, 95% CI "
                f"[{self.low:.1%}, {self.high:.1%}]; independent games would need {gain:.1f}x as "
                f"many for this interval){f', {self.ties} ties' if self.ties else ''}"
                f"{f', {self.misplaced} games on unexpected teams' if self.misplaced else ''}")


def pair_scores(battles: Iterable, pairs: TeamPairs) -> PairedResult:
    """A's results (`battles`: A's finished battles), pair by pair."""
    halves: Dict[int, List[float]] = {}
    games = ties = misplaced = 0
    for battle in battles:
        if not battle.finished:
            continue
        games += 1
        score = battle_score(battle)
        ties += score == 0.5
        where = pairs.locate(frozenset(key.split(": ", 1)[-1] for key in battle.team))
        if where is None:
            misplaced += 1
            continue
        halves.setdefault(where[0], []).append(score)
    scores = [sum(h) / 2 for h in halves.values() if len(h) == 2]
    return summarize(scores, games, ties, misplaced)


def summarize(scores: Sequence[float], games: int, ties: int = 0, misplaced: int = 0) -> PairedResult:
    """The paired result of these pair scores (0, 0.5 or 1 each: A's mean over a pair)."""
    n = len(scores)
    mean = sum(scores) / n if n else float("nan")
    var = sum((s - mean) ** 2 for s in scores) / (n - 1) if n > 1 else float("nan")
    half = 1.96 * math.sqrt(var / n) if n > 1 else float("nan")
    unpaired = 1.96 * math.sqrt(mean * (1 - mean) / (2 * n)) if n else float("nan")
    return PairedResult(mean, max(0.0, mean - half), min(1.0, mean + half), n, games, ties,
                        misplaced, unpaired, list(scores))
