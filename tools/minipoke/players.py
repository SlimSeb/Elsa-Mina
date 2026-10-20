"""
poke-env players.

All battles run against ONE local Showdown server. poke-env drives them from a
single asyncio loop (POKE_LOOP, in a background thread), and many battles run
concurrently through ``max_concurrent_battles``. ``choose_move`` is async, so
every pending decision is pushed into an ``InferenceBatcher`` that groups them
into one forward pass. This replaces ps-ppo's multi-server / Ray setup.
"""

from __future__ import annotations

import asyncio
import random
import threading
from concurrent.futures import ThreadPoolExecutor
from typing import Dict, List, Optional, Tuple

import numpy as np
import torch
from poke_env.player import Player, SimpleHeuristicsPlayer

from encoder import (
    action_to_order,
    collate,
    encode,
    fainted_counts,
    order_to_compact_action,
)
from model import to_tensors

FAINT_REWARD = 0.1
WIN_REWARD = 1.0


# --------------------------------------------------------------------------
# Batched inference
# --------------------------------------------------------------------------
class InferenceBatcher:
    """Collects decisions from concurrent battles and runs them as one batch.

    The model here is the *inference copy* (usually on CPU: for batches of a
    few dozen states, CPU beats MPS dispatch overhead on an M-series chip).
    """

    def __init__(self, model: torch.nn.Module, device: torch.device,
                 max_batch: int = 256, max_wait_ms: float = 2.0):
        self.model = model.eval()
        self.device = device
        self.max_batch = max_batch
        self.max_wait = max_wait_ms / 1000.0
        self._queue: Optional[asyncio.Queue] = None
        self._worker: Optional[asyncio.Task] = None
        # One thread does all torch work so the event loop stays responsive.
        self._executor = ThreadPoolExecutor(max_workers=1)

    async def act(self, obs: Dict[str, np.ndarray], greedy: bool = False) -> Tuple[int, float, float]:
        if self._queue is None:  # lazily bind to poke-env's loop
            self._queue = asyncio.Queue()
            self._worker = asyncio.get_running_loop().create_task(self._run())
        fut = asyncio.get_running_loop().create_future()
        await self._queue.put((obs, greedy, fut))
        return await fut

    async def _run(self) -> None:
        loop = asyncio.get_running_loop()
        while True:
            items = [await self._queue.get()]
            deadline = loop.time() + self.max_wait
            while len(items) < self.max_batch:
                timeout = deadline - loop.time()
                if timeout <= 0:
                    break
                try:
                    items.append(await asyncio.wait_for(self._queue.get(), timeout))
                except asyncio.TimeoutError:
                    break
            try:
                results = await loop.run_in_executor(self._executor, self._forward, items)
                for (_, _, fut), res in zip(items, results):
                    if not fut.done():
                        fut.set_result(res)
            except Exception as e:  # never kill the worker
                for _, _, fut in items:
                    if not fut.done():
                        fut.set_exception(e)

    @torch.no_grad()
    def _forward(self, items) -> List[Tuple[int, float, float]]:
        batch = to_tensors(collate([o for o, _, _ in items]), self.device)
        logits, values = self.model(batch)
        dist = torch.distributions.Categorical(logits=logits)
        sampled = dist.sample()
        greedy = torch.tensor([g for _, g, _ in items], device=logits.device)
        actions = torch.where(greedy, logits.argmax(-1), sampled)
        logps = dist.log_prob(actions)
        return list(zip(actions.tolist(), logps.tolist(), values.tolist()))


# --------------------------------------------------------------------------
# Experience storage (filled from the poke-env thread, read from main thread)
# --------------------------------------------------------------------------
class EpisodeCollector:
    def __init__(self):
        self._lock = threading.Lock()
        self._episodes: List[List[dict]] = []

    def add(self, episode: List[dict]) -> None:
        if episode:
            with self._lock:
                self._episodes.append(episode)

    def drain(self) -> List[List[dict]]:
        with self._lock:
            eps, self._episodes = self._episodes, []
        return eps


# --------------------------------------------------------------------------
# RL player
# --------------------------------------------------------------------------
class RLPlayer(Player):
    """Plays with the network. If ``collector`` is set, records trajectories."""

    def __init__(self, batcher: InferenceBatcher, collector: Optional[EpisodeCollector] = None,
                 greedy: bool = False, **kwargs):
        super().__init__(**kwargs)
        self.batcher = batcher
        self.collector = collector
        self.greedy = greedy
        self._traj: Dict[str, List[dict]] = {}
        self._faints: Dict[str, Tuple[int, int]] = {}

    async def choose_move(self, battle):
        obs = encode(battle)
        if not obs["mask"].any():
            return self.choose_random_move(battle)

        action, logp, value = await self.batcher.act(obs, self.greedy)

        if self.collector is not None:
            tag = battle.battle_tag
            traj = self._traj.setdefault(tag, [])
            mine, theirs = fainted_counts(battle)
            if traj:  # reward for the previous decision = faint delta since then
                pm, pt = self._faints[tag]
                traj[-1]["reward"] += FAINT_REWARD * ((theirs - pt) - (mine - pm))
            self._faints[tag] = (mine, theirs)
            traj.append({"obs": obs, "action": action, "logp": logp,
                         "value": value, "reward": 0.0})

        return action_to_order(action, battle)

    def _battle_finished_callback(self, battle) -> None:
        if self.collector is None:
            return
        tag = battle.battle_tag
        traj = self._traj.pop(tag, [])
        if not traj:
            return
        mine, theirs = fainted_counts(battle)
        pm, pt = self._faints.pop(tag, (mine, theirs))
        traj[-1]["reward"] += FAINT_REWARD * ((theirs - pt) - (mine - pm))
        if battle.won:
            traj[-1]["reward"] += WIN_REWARD
        elif battle.lost:
            traj[-1]["reward"] -= WIN_REWARD
        self.collector.add(traj)


# --------------------------------------------------------------------------
# Teacher for behavioural cloning
# --------------------------------------------------------------------------
class TeacherPlayer(Player):
    """Plays SimpleHeuristicsPlayer's moves and records (obs, action) pairs.

    ``epsilon`` plays a random legal move some of the time so the dataset also
    covers states the heuristic would never reach on its own.
    """

    def __init__(self, dataset: list, lock: threading.Lock, epsilon: float = 0.1, **kwargs):
        super().__init__(**kwargs)
        self.dataset = dataset
        self.lock = lock
        self.epsilon = epsilon

    def choose_move(self, battle):
        order, _ = SimpleHeuristicsPlayer.choose_singles_move(battle)
        obs = encode(battle)
        action = order_to_compact_action(order, battle)
        if action is not None and obs["mask"][action]:
            with self.lock:
                self.dataset.append((obs, action))
        if random.random() < self.epsilon:
            return self.choose_random_singles_move(battle)
        return order
