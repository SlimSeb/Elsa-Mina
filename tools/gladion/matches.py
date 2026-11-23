"""
Matches on the single local Showdown server, for `train.py eval` and `train.py elo`: a model
(greedy, or searching) against a scripted player or another model, on paired teams
(paired.py) or independent random battles. A watchdog ends a match when no battle finishes
for `stall_timeout` seconds or it runs past its time limit.
"""

from __future__ import annotations

import asyncio
import copy
import logging
import math
import time
from dataclasses import dataclass
from typing import Callable, List, Optional, Sequence, Tuple

import torch
from poke_env.player import MaxBasePowerPlayer, Player, RandomPlayer, SimpleHeuristicsPlayer

from paired import PairedResult, TeamPairs, make_paired, pair_scores
from players import InferenceBatcher, ModelPlayer

log = logging.getLogger("gladion")
FORMAT = "gen9randombattle"


def make_kwargs(concurrency: int) -> dict:
    return dict(
        battle_format=FORMAT,
        max_concurrent_battles=max(1, concurrency),
        log_level=logging.WARNING,
        open_timeout=None,
    )


class _Scripted:
    """A scripted player that never stalls a battle on an exception and reports
    finished battles to the watchdog."""

    def __init__(self, **kwargs):
        super().__init__(**kwargs)
        self.n_done = 0
        self.n_won = 0

    def choose_move(self, battle):
        try:
            return super().choose_move(battle)
        except Exception:
            log.exception("%s choose_move failed in %s; playing randomly", type(self).__name__,
                          battle.battle_tag)
            return self.choose_random_move(battle)

    def _battle_finished_callback(self, battle) -> None:
        self.n_done += 1
        self.n_won += int(bool(battle.won))


class SafeHeuristicsPlayer(_Scripted, SimpleHeuristicsPlayer):
    pass


class SafeMaxPowerPlayer(_Scripted, MaxBasePowerPlayer):
    pass


class SafeRandomPlayer(_Scripted, RandomPlayer):
    pass


# The scripted players, by the names Elo tables and `eval --opponent` use
SCRIPTED = {"heuristic": SafeHeuristicsPlayer, "maxpower": SafeMaxPowerPlayer,
            "random": SafeRandomPlayer}


def frozen_copy(model: torch.nn.Module, device) -> torch.nn.Module:
    m = copy.deepcopy(model).to(device).eval()
    for p in m.parameters():
        p.requires_grad_(False)
    return m


# --------------------------------------------------------------------------
# Running pairs with a watchdog
# --------------------------------------------------------------------------
@dataclass
class PlayResult:
    ok: bool
    seconds: float
    reason: str = ""


def _done(players: Sequence[Player]) -> int:
    return sum(getattr(p, "n_done", 0) for p in players)


async def _play_pairs(pairs: List[Tuple[Player, Player, int]], watched: Sequence[Player],
                      stall_timeout: float, timeout: float) -> PlayResult:
    t0 = time.monotonic()
    tasks = [asyncio.ensure_future(a.battle_against(b, n_battles=n)) for a, b, n in pairs if n > 0]
    if not tasks:
        return PlayResult(True, 0.0)
    last_count, last_progress = _done(watched), time.monotonic()
    reason = ""
    while True:
        done, pending = await asyncio.wait(tasks, timeout=5.0)
        failed = [t for t in done if not t.cancelled() and t.exception() is not None]
        if failed:
            reason = f"battle task failed: {failed[0].exception()!r}"
            break
        if not pending:
            return PlayResult(True, time.monotonic() - t0)
        now = time.monotonic()
        count = _done(watched)
        if count != last_count:
            last_count, last_progress = count, now
        if now - last_progress > stall_timeout:
            reason = f"no battle finished for {now - last_progress:.0f}s"
            break
        if now - t0 > timeout:
            reason = f"the match exceeded its {timeout:.0f}s limit"
            break
    for t in tasks:
        t.cancel()
    await asyncio.gather(*tasks, return_exceptions=True)
    return PlayResult(False, time.monotonic() - t0, reason)


async def _close(players: Sequence[Player]) -> None:
    async def close(p):
        try:
            await asyncio.wait_for(p.ps_client.stop_listening(), 10)
        except Exception:
            pass
    await asyncio.gather(*(close(p) for p in players))


def close_players(players: Sequence[Player]) -> None:
    asyncio.run(_close(players))


@dataclass
class MatchResult:
    """A's results in a match. A tie (Showdown's turn 1000 auto-tie) counts as half a win."""
    score: float                          # wins + ties / 2
    games: int                            # finished games
    ties: int = 0
    paired: Optional[PairedResult] = None  # paired teams: the pair-by-pair result

    @property
    def rate(self) -> float:
        return self.score / self.games if self.games else 0.0

    def describe(self) -> str:
        if self.paired is not None:
            return self.paired.describe()
        lo, hi = wilson_interval(self.score, self.games)
        return (f"{self.rate:.1%} over {self.games} battles (95% CI [{lo:.1%}, {hi:.1%}])"
                + (f", {self.ties} ties" if self.ties else ""))


def run_match(a: Player, b: Player, n_battles: int, stall_timeout: float = 180.0, server=None,
              pairs: Optional[TeamPairs] = None, what: str = "match") -> MatchResult:
    """`a` challenges `b` `n_battles` times (with `pairs`: twice per team pair, the teams
    swapped the second time), then both are closed."""
    if pairs is not None:
        make_paired(a, pairs.challenger())
        make_paired(b, pairs.accepter())
        n_battles = 2 * len(pairs)
    res = asyncio.run(_play_pairs([(a, b, n_battles)], [a, b], stall_timeout,
                                  max(600.0, n_battles * 10.0)))
    if not res.ok:
        log.error("%s stalled: %s", what, res.reason)
        if server is not None:
            server.ensure_up()
    paired = pair_scores(a.battles.values(), pairs) if pairs is not None else None
    close_players([a, b])
    ties = max(0, a.n_done - a.n_won - getattr(b, "n_won", 0))
    return MatchResult(a.n_won + ties / 2, a.n_done, ties, paired)


def evaluate(batcher: InferenceBatcher, make_opponent: Callable[[], Player], n_battles: int,
             concurrency: int, stall_timeout: float = 180.0, server=None,
             no_tera_before: int = 0, pairs: Optional[TeamPairs] = None,
             player: Optional[Player] = None) -> MatchResult:
    """Greedy play with `batcher`'s model (or `player`, e.g. a SearchPlayer) against a
    fresh opponent."""
    me = player or ModelPlayer(batcher, no_tera_before=no_tera_before, **make_kwargs(concurrency))
    return run_match(me, make_opponent(), n_battles, stall_timeout, server, pairs, "evaluation")


def wilson_interval(wins: float, n: int, z: float = 1.96) -> Tuple[float, float]:
    """95% (z = 1.96) Wilson score interval of a win rate. (0, 1) without battles."""
    if n <= 0:
        return 0.0, 1.0
    p = wins / n
    denom = 1 + z * z / n
    centre = (p + z * z / (2 * n)) / denom
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / denom
    return max(0.0, centre - half), min(1.0, centre + half)
