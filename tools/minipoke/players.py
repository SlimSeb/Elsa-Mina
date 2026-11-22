"""
poke-env players that play a model, for evaluation (train.py eval / elo) and the search
(search.SearchPlayer).

All battles run against ONE local Showdown server. poke-env drives them from a
single asyncio loop (POKE_LOOP, in a background thread), and many battles run
concurrently through ``max_concurrent_battles``. ``choose_move`` is async, so
every pending decision is pushed into an ``InferenceBatcher`` that groups them
into one forward pass.
"""

from __future__ import annotations

import asyncio
import logging
from concurrent.futures import ThreadPoolExecutor
from typing import Dict, List, Optional, Tuple

import numpy as np
import torch
from poke_env.player import Player

from encoder import action_to_order, collate
from encoder_v2 import model_encoder
from model import to_tensors

# The faint reward of the returns the value head learns (human_data.py's --faint-reward): the
# search adds it to each simulated turn's value
FAINT_REWARD = 0.1
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
# A model's player
# --------------------------------------------------------------------------
class ModelPlayer(SafePlayer):
    """Plays the model's most likely legal action. ``no_tera_before``: never terastallize
    before that turn (evaluation of early tera)."""

    def __init__(self, batcher: InferenceBatcher, no_tera_before: int = 0, **kwargs):
        super().__init__(**kwargs)
        self.batcher = batcher
        self.no_tera_before = no_tera_before

    async def _choose(self, battle):
        obs = self.batcher.encoder.encode(battle)
        if not obs["mask"].any():
            return self.choose_random_move(battle)
        if battle.turn < self.no_tera_before:
            obs = {**obs, "mask": obs["mask"].copy()}
            obs["mask"][E_TERA:] = False
        action, _, _, _ = await self.batcher.act(obs, greedy=True)
        return action_to_order(action, battle)
