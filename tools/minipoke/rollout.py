"""
Who plays whom in one PPO iteration, on the single local Showdown server.

    learner A  vs  learner B            mirror: both sides are trained on
    learner    vs  pool slot i          a past snapshot (own model + batcher)
    learner    vs  SimpleHeuristics

Every learner seat shares one InferenceBatcher (one model), each pool slot has
its own, so batches stay large. Only learner seats record trajectories. All
pairs play at the same time; a watchdog cancels the iteration if no battle
finishes for `stall_timeout` seconds or it runs past its time limit, then
the players are rebuilt (new websockets) and training goes on.
"""

from __future__ import annotations

import asyncio
import copy
import logging
import math
import statistics
import time
from dataclasses import dataclass, field
from typing import Callable, Dict, List, Optional, Sequence, Tuple

import torch
from poke_env.player import Player, SimpleHeuristicsPlayer

from league import split_evenly
from players import EpisodeCollector, InferenceBatcher, Rewards, RLPlayer, SafePlayer

log = logging.getLogger("minipoke")
FORMAT = "gen9randombattle"


def make_kwargs(concurrency: int) -> dict:
    return dict(
        battle_format=FORMAT,
        max_concurrent_battles=max(1, concurrency),
        log_level=logging.WARNING,
        open_timeout=None,
    )


class SafeHeuristicsPlayer(SimpleHeuristicsPlayer):
    """SimpleHeuristicsPlayer that never stalls a battle on an exception and
    reports finished battles to the watchdog."""

    def __init__(self, **kwargs):
        super().__init__(**kwargs)
        self.n_done = 0
        self.n_won = 0

    def choose_move(self, battle):
        try:
            return super().choose_move(battle)
        except Exception:
            log.exception("heuristic choose_move failed in %s; playing randomly", battle.battle_tag)
            return self.choose_random_move(battle)

    def _battle_finished_callback(self, battle) -> None:
        self.n_done += 1
        self.n_won += int(bool(battle.won))


def frozen_copy(model: torch.nn.Module, device) -> torch.nn.Module:
    m = copy.deepcopy(model).to(device).eval()
    for p in m.parameters():
        p.requires_grad_(False)
    return m


def load_into(dst: torch.nn.Module, state: Dict[str, torch.Tensor]) -> None:
    device = next(dst.parameters()).device
    dst.load_state_dict({k: v.to(device) for k, v in state.items()})


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
            reason = f"iteration exceeded its {timeout:.0f}s limit"
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


def reset_all(players: Sequence[Player]) -> bool:
    try:
        for p in players:
            p.reset_battles()
            p.n_done = p.n_won = 0
        return True
    except EnvironmentError:
        return False


# --------------------------------------------------------------------------
@dataclass
class RolloutConfig:
    concurrency: int = 32
    mirror_share: float = 1.0
    pool_share: float = 0.0
    heuristic_share: float = 0.0
    pool_slots: int = 2
    max_infer_batch: int = 64   # CPU throughput peaks around here for the medium model
    stall_timeout: float = 180.0
    min_iter_timeout: float = 600.0
    iter_timeout_factor: float = 4.0


@dataclass
class IterationResult:
    episodes: list
    play: PlayResult
    win_rates: Dict[str, Optional[float]] = field(default_factory=dict)
    # Learner (wins, finished battles) against each pool slot, in slot order
    slot_results: List[Tuple[int, int]] = field(default_factory=list)


class Rollouts:
    def __init__(self, learner: torch.nn.Module, device: torch.device, cfg: RolloutConfig,
                 server=None, rewards: Optional[Rewards] = None):
        self.cfg = cfg
        self.device = device
        self.server = server
        self.learner = learner
        self.rewards = rewards or Rewards()  # shared by every learner seat
        self.batcher = InferenceBatcher(learner, device,
                                        max_batch=min(cfg.concurrency * 2, cfg.max_infer_batch))
        self.collector = EpisodeCollector()
        n_slots = cfg.pool_slots if cfg.pool_share > 0 else 0
        self.slot_models = [frozen_copy(learner, device) for _ in range(n_slots)]
        self.slot_batchers = [InferenceBatcher(m, device, max_batch=min(cfg.concurrency, cfg.max_infer_batch))
                              for m in self.slot_models]
        self.eval_model = frozen_copy(learner, device)
        self.eval_batcher = InferenceBatcher(self.eval_model, device,
                                             max_batch=min(cfg.concurrency * 2, cfg.max_infer_batch))
        self.play_times: List[float] = []
        self.rebuilds = 0
        self._build()

    # concurrency per pair, proportional to the share of battles it plays
    def _conc(self, share: float) -> int:
        total = self.cfg.mirror_share + self.cfg.pool_share + self.cfg.heuristic_share
        return max(1, round(self.cfg.concurrency * share / total))

    def _learner(self, concurrency: int) -> RLPlayer:
        return RLPlayer(self.batcher, self.collector, rewards=self.rewards,
                        **make_kwargs(concurrency))

    def _build(self) -> None:
        c = self.cfg
        self.mirror = (self._learner(self._conc(c.mirror_share)),
                       self._learner(self._conc(c.mirror_share)))
        slot_conc = self._conc(c.pool_share / max(len(self.slot_models), 1))
        self.pool_pairs = [(self._learner(slot_conc), RLPlayer(b, None, **make_kwargs(slot_conc)))
                           for b in self.slot_batchers]
        self.heuristic_pair = None
        if c.heuristic_share > 0:
            hc = self._conc(c.heuristic_share)
            self.heuristic_pair = (self._learner(hc), SafeHeuristicsPlayer(**make_kwargs(hc)))

    @property
    def players(self) -> List[Player]:
        ps = list(self.mirror) + [p for pair in self.pool_pairs for p in pair]
        return ps + (list(self.heuristic_pair) if self.heuristic_pair else [])

    def rebuild(self) -> None:
        """Drops every websocket and battle in flight and logs in fresh players."""
        self.rebuilds += 1
        log.warning("rebuilding players (#%d)", self.rebuilds)
        close_players(self.players)
        self.collector.drain()
        if self.server is not None:
            self.server.ensure_up()
        self._build()

    def load_slots(self, states: List[Dict[str, torch.Tensor]]) -> None:
        for i, m in enumerate(self.slot_models):
            load_into(m, states[i % len(states)])

    def iter_timeout(self) -> float:
        recent = self.play_times[-10:]
        if not recent:
            return max(self.cfg.min_iter_timeout, 3600.0)
        return max(self.cfg.min_iter_timeout, self.cfg.iter_timeout_factor * statistics.median(recent))

    def play_iteration(self, n_mirror: int, n_pool: int, n_heuristic: int) -> IterationResult:
        pairs = [(self.mirror[0], self.mirror[1], n_mirror)]
        if self.pool_pairs:
            pairs += [(a, b, n) for (a, b), n in zip(self.pool_pairs,
                                                     split_evenly(n_pool, len(self.pool_pairs)))]
        if self.heuristic_pair:
            pairs.append((*self.heuristic_pair, n_heuristic))
        res = asyncio.run(_play_pairs(pairs, self.players, self.cfg.stall_timeout, self.iter_timeout()))
        win_rates = {"mirror": None,
                     "pool": _win_rate([a for a, _ in self.pool_pairs]),
                     "heuristic": _win_rate([self.heuristic_pair[0]] if self.heuristic_pair else [])}
        slot_results = [(a.n_won, a.n_done) for a, _ in self.pool_pairs]
        if res.ok:
            self.play_times.append(res.seconds)
        if not res.ok:
            log.error("iteration stalled: %s", res.reason)
        # Finished battles are complete episodes: keep them even after a stall.
        episodes = self.collector.drain()
        if not res.ok or not reset_all(self.players):
            self.rebuild()
        return IterationResult(episodes, res, win_rates, slot_results)

    # ----------------------------------------------------------------------
    def evaluate(self, n_battles: int, opponent: str = "heuristic",
                 snapshot_state: Optional[Dict[str, torch.Tensor]] = None) -> Tuple[float, int]:
        """Greedy learner vs the heuristic or a (greedy) snapshot. -> (win rate, finished)."""
        if opponent == "heuristic":
            make_opp = lambda: SafeHeuristicsPlayer(**make_kwargs(self.cfg.concurrency))
        else:
            load_into(self.eval_model, snapshot_state)
            make_opp = lambda: RLPlayer(self.eval_batcher, None, greedy=True,
                                        **make_kwargs(self.cfg.concurrency))
        return evaluate(self.batcher, make_opp, n_battles, self.cfg.concurrency,
                        self.cfg.stall_timeout, self.server)


def evaluate(batcher: InferenceBatcher, make_opponent: Callable[[], Player], n_battles: int,
             concurrency: int, stall_timeout: float = 180.0, server=None) -> Tuple[float, int]:
    """Greedy play with `batcher`'s model against a fresh opponent. -> (win rate, finished)."""
    me = RLPlayer(batcher, None, greedy=True, **make_kwargs(concurrency))
    opp = make_opponent()
    res = asyncio.run(_play_pairs([(me, opp, n_battles)], [me], stall_timeout,
                                  max(600.0, n_battles * 10.0)))
    if not res.ok:
        log.error("evaluation stalled: %s", res.reason)
        if server is not None:
            server.ensure_up()
    close_players([me, opp])
    return me.n_won / max(me.n_done, 1), me.n_done


def wilson_interval(wins: float, n: int, z: float = 1.96) -> Tuple[float, float]:
    """95% (z = 1.96) Wilson score interval of a win rate. (0, 1) without battles."""
    if n <= 0:
        return 0.0, 1.0
    p = wins / n
    denom = 1 + z * z / n
    centre = (p + z * z / (2 * n)) / denom
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / denom
    return max(0.0, centre - half), min(1.0, centre + half)


def _win_rate(players: Sequence[SafePlayer]) -> Optional[float]:
    done = sum(p.n_done for p in players)
    return sum(p.n_won for p in players) / done if done else None


def play_recording_pairs(make_pair: Callable[[], Tuple[Player, Player]], n_battles: int,
                         chunk: int, stall_timeout: float = 180.0, server=None,
                         stop=None) -> int:
    """For BC / distillation data: plays `n_battles` in chunks, rebuilding the
    pair after a stall. Returns the number of finished battles."""
    a, b = make_pair()
    finished = 0
    while finished < n_battles and not (stop is not None and stop.stop):
        n = min(chunk, n_battles - finished)
        res = asyncio.run(_play_pairs([(a, b, n)], [a], stall_timeout, max(600.0, n * 20.0)))
        finished += a.n_done
        log.info("[data] %d / %d battles (%.0fs for this chunk)", finished, n_battles, res.seconds)
        if not res.ok or not reset_all([a, b]):
            log.error("data collection stalled: %s; rebuilding players", res.reason)
            close_players([a, b])
            if server is not None:
                server.ensure_up()
            a, b = make_pair()
    close_players([a, b])
    return finished
