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
import logging
import random
import threading
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
from typing import Dict, List, Optional, Tuple

import numpy as np
import torch
from poke_env.player import Player, SimpleHeuristicsPlayer

from encoder import (
    action_to_order,
    collate,
    fainted_counts,
    order_to_compact_action,
)
from encoder_v2 import EncoderV1, model_encoder
from dataset import ShardWriter, discounted_returns
from model import to_tensors

FAINT_REWARD = 0.1
WIN_REWARD = 1.0
E_TERA = 10  # first terastallize + move action

log = logging.getLogger("minipoke")


# --------------------------------------------------------------------------
# Batched inference
# --------------------------------------------------------------------------
class InferenceBatcher:
    """Collects decisions from concurrent battles and runs them as one batch.

    The model here is the *inference copy* (usually on CPU: for batches of a
    few dozen states, CPU beats MPS dispatch overhead on an M-series chip).
    `encoder` builds that model's observations (V1 or v2): players feeding this
    batcher encode with it.
    """

    def __init__(self, model: torch.nn.Module, device: torch.device,
                 max_batch: int = 256, max_wait_ms: float = 2.0):
        self.model = model.eval()
        self.encoder = model_encoder(model)
        self.device = device
        self.max_batch = max_batch
        self.max_wait = max_wait_ms / 1000.0
        self._queue: Optional[asyncio.Queue] = None
        self._worker: Optional[asyncio.Task] = None
        # One thread does all torch work so the event loop stays responsive.
        self._executor = ThreadPoolExecutor(max_workers=1)

    async def act(self, obs: Dict[str, np.ndarray], greedy: bool = False
                  ) -> Tuple[int, float, float, np.ndarray]:
        """-> (action, log-prob of the action, value, full action distribution)."""
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
    def _forward(self, items) -> List[Tuple[int, float, float, np.ndarray]]:
        batch = to_tensors(collate([o for o, _, _ in items]), self.device)
        logits, values = self.model(batch)
        dist = torch.distributions.Categorical(logits=logits)
        sampled = dist.sample()
        greedy = torch.tensor([g for _, g, _ in items], device=logits.device)
        actions = torch.where(greedy, logits.argmax(-1), sampled)
        logps = dist.log_prob(actions)
        probs = dist.probs.float().cpu().numpy()
        return list(zip(actions.tolist(), logps.tolist(), values.tolist(), probs))


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
# Rewards: win +-1 at the end, +-0.1 per faint (theirs - ours) after each decision
# --------------------------------------------------------------------------
@dataclass
class Rewards:
    """The defaults are V1's. Players share one instance, so changing `faint`
    between iterations (faint annealing) applies to every learner seat."""
    win: float = WIN_REWARD
    loss: float = -WIN_REWARD
    faint: float = FAINT_REWARD

    @classmethod
    def named(cls, name: str) -> "Rewards":
        """"shaped": V1's (+-1, +-0.1 per faint). "winloss": 1 for a win, 0 otherwise,
        nothing else (trained with gamma 1, undiscounted)."""
        return cls() if name == "shaped" else cls(win=1.0, loss=0.0, faint=0.0)


def faint_reward(total_states: int, anneal_states: Optional[int], progress: float = 0.0,
                 anneal_until: float = 0.0) -> float:
    """FAINT_REWARD, taken linearly to 0 over `anneal_states` training states or, without
    them, until the run's progress (the LR and entropy schedules' fraction of the budget)
    reaches `anneal_until`. Neither: constant, as in V1."""
    if anneal_states:
        return FAINT_REWARD * max(0.0, 1.0 - total_states / anneal_states)
    if anneal_until and anneal_until > 0:
        return FAINT_REWARD * max(0.0, 1.0 - progress / anneal_until)
    return FAINT_REWARD


class Trajectories:
    """Per-battle step lists. The reward of a step is the faint delta until the
    next decision, plus the result on the last step."""

    def __init__(self, rewards: Optional[Rewards] = None):
        self.rewards = rewards or Rewards()
        self._traj: Dict[str, List[dict]] = {}
        self._faints: Dict[str, Tuple[int, int]] = {}

    def step(self, battle, record: dict) -> None:
        tag = battle.battle_tag
        traj = self._traj.setdefault(tag, [])
        mine, theirs = fainted_counts(battle)
        if traj:  # reward for the previous decision = faint delta since then
            pm, pt = self._faints[tag]
            traj[-1]["reward"] += self.rewards.faint * ((theirs - pt) - (mine - pm))
        self._faints[tag] = (mine, theirs)
        record["reward"] = 0.0
        traj.append(record)

    def finish(self, battle) -> List[dict]:
        tag = battle.battle_tag
        traj = self._traj.pop(tag, [])
        if not traj:
            self._faints.pop(tag, None)
            return traj
        mine, theirs = fainted_counts(battle)
        pm, pt = self._faints.pop(tag, (mine, theirs))
        traj[-1]["reward"] += self.rewards.faint * ((theirs - pt) - (mine - pm))
        if battle.won:
            traj[-1]["reward"] += self.rewards.win
        elif battle.lost:
            traj[-1]["reward"] += self.rewards.loss
        return traj


class SafePlayer(Player):
    """An exception in choose_move would leave the battle waiting forever (poke-env
    re-raises it in its message task). Log it and play a random move instead."""

    def __init__(self, **kwargs):
        super().__init__(**kwargs)
        self.n_errors = 0
        self.n_done = 0   # finished battles (the watchdog's progress signal)
        self.n_won = 0

    async def choose_move(self, battle):
        try:
            return await self._choose(battle)
        except Exception:
            self.n_errors += 1
            if self.n_errors <= 5 or self.n_errors % 100 == 0:
                log.exception("[%s] choose_move failed in %s (%d errors so far); playing randomly",
                              self.username, battle.battle_tag, self.n_errors)
            return self.choose_random_move(battle)

    async def _choose(self, battle):
        raise NotImplementedError

    def _battle_finished_callback(self, battle) -> None:
        self.n_done += 1
        self.n_won += int(bool(battle.won))
        try:
            self._on_finished(battle)
        except Exception:
            log.exception("[%s] end-of-battle bookkeeping failed in %s",
                          self.username, battle.battle_tag)

    def _on_finished(self, battle) -> None:
        pass


# --------------------------------------------------------------------------
# RL player
# --------------------------------------------------------------------------
class RLPlayer(SafePlayer):
    """Plays with the network. If ``collector`` is set, records trajectories.
    ``no_tera_before``: never terastallize before that turn (evaluation only)."""

    def __init__(self, batcher: InferenceBatcher, collector: Optional[EpisodeCollector] = None,
                 greedy: bool = False, rewards: Optional[Rewards] = None, labeler=None,
                 no_tera_before: int = 0, **kwargs):
        super().__init__(**kwargs)
        self.batcher = batcher
        self.collector = collector
        self.greedy = greedy
        self.no_tera_before = no_tera_before
        self.labeler = labeler   # aux_labels.AuxLabeler: report decisions and sets to it
        self._trajectories = Trajectories(rewards)

    async def _choose(self, battle):
        obs = self.batcher.encoder.encode(battle)
        if not obs["mask"].any():
            return self.choose_random_move(battle)
        if battle.turn < self.no_tera_before:
            obs = {**obs, "mask": obs["mask"].copy()}
            obs["mask"][E_TERA:] = False

        action, logp, value, _ = await self.batcher.act(obs, self.greedy)

        ctx = self.labeler.decision(battle, action, obs["mask"]) if self.labeler else None
        if self.collector is not None:
            record = {"obs": obs, "action": action, "logp": logp, "value": value}
            if ctx is not None:
                record["aux_ctx"] = ctx
            self._trajectories.step(battle, record)
        return action_to_order(action, battle)

    def _on_finished(self, battle) -> None:
        if self.collector is not None:
            self.collector.add(self._trajectories.finish(battle))


# --------------------------------------------------------------------------
# Teachers for behavioural cloning / distillation. Each battle's samples are
# written once it ends, with the discounted return from every decision.
# --------------------------------------------------------------------------
class _RecordingPlayer(SafePlayer):
    """`encoder` builds the recorded observations: the student's."""

    def __init__(self, writer: ShardWriter, gamma: float, epsilon: float, encoder=None, **kwargs):
        super().__init__(**kwargs)
        self.writer = writer
        self.gamma = gamma
        self.epsilon = epsilon
        self.encoder = encoder or EncoderV1()
        self._trajectories = Trajectories()

    def _record(self, battle, obs, probs, action, value=0.0) -> None:
        self._trajectories.step(battle, {"obs": obs, "probs": probs, "action": action,
                                         "value": value})

    def _on_finished(self, battle) -> None:
        traj = self._trajectories.finish(battle)
        rets = discounted_returns([s["reward"] for s in traj], self.gamma)
        for s, r in zip(traj, rets):
            self.writer.add(s["obs"], s["probs"], s["action"], s["value"], float(r))


class TeacherPlayer(_RecordingPlayer):
    """Plays SimpleHeuristicsPlayer's moves and records (obs, one-hot action).

    ``epsilon`` plays a random legal move some of the time so the dataset also
    covers states the heuristic would never reach on its own.
    """

    async def _choose(self, battle):
        order, _ = SimpleHeuristicsPlayer.choose_singles_move(battle)
        obs = self.encoder.encode(battle)
        action = order_to_compact_action(order, battle)
        if action is not None and obs["mask"][action]:
            probs = np.zeros(len(obs["mask"]), dtype=np.float32)
            probs[action] = 1.0
            self._record(battle, obs, probs, action)
        if random.random() < self.epsilon:
            return self.choose_random_singles_move(battle)
        return order


class DistillPlayer(_RecordingPlayer):
    """Plays a trained model's sampled moves (a uniformly random legal move with
    probability ``epsilon``) and records the model's full distribution and value.

    The teacher (``batcher``'s model) sees the battle through its own encoder, the
    recorded sample is the student's ``encoder``'s view of the same state: a V1
    teacher can train a v2 student. Every encoder shares V1's action space and mask.
    """

    def __init__(self, batcher: InferenceBatcher, writer: ShardWriter, gamma: float,
                 epsilon: float, encoder=None, **kwargs):
        super().__init__(writer, gamma, epsilon, encoder=encoder or batcher.encoder, **kwargs)
        self.batcher = batcher

    async def _choose(self, battle):
        obs = self.batcher.encoder.encode(battle)
        if not obs["mask"].any():
            return self.choose_random_move(battle)
        action, _, value, probs = await self.batcher.act(obs)
        if random.random() < self.epsilon:
            action = int(random.choice(np.flatnonzero(obs["mask"])))
        student = obs if self.encoder is self.batcher.encoder else self.encoder.encode(battle)
        self._record(battle, student, probs, action, value)
        return action_to_order(action, battle)
