"""
On-disk dataset for behavioural cloning and distillation.

A float32 observation is ~12 KB, so 10k+ battles (~500k decisions) would not fit
in 16 GB as a list of numpy dicts. Samples are written instead to compressed
.npz shards of `shard_size` rows, in compact dtypes:

    mon_ids, move_ids         int16   (largest vocabulary: 1602 species)
    mon_feats, move_feats,    float16 (features are in [-1, 2]; ~5e-4 relative error)
    field
    mask                      uint8   (14 bools bit-packed into 2 bytes)

(that is encoder V1's observation; any other encoder's arrays are stored the same
way by dtype, ids as int16, features as float16, bools bit-packed, and meta.json
records the observation layout)
    probs                     float16 [14]  teacher action distribution (one-hot for the heuristic)
    value                     float16 teacher value (0 when the teacher has none)
    ret                       float16 discounted return of the battle from that decision
    action                    int8    action actually played

They are mostly zeros (one-hots, empty move slots), so they compress well.
Training streams them: shards are shuffled, `buffer_shards` are decoded at a
time and their rows shuffled together, so RAM stays at a few hundred MB.
The tail `val_frac` of every shard is the validation split.
"""

from __future__ import annotations

import json
import threading
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Dict, Iterator, List, Optional

import numpy as np

from encoder import ACT_DIM
from encoder_v2 import V1_LAYOUT, Layout

OBS_KEYS = list(V1_LAYOUT)
STORED = {"int64": np.int16, "float32": np.float16}
TARGETS = {
    "probs": ((ACT_DIM,), np.float16),
    "value": ((), np.float16),
    "ret": ((), np.float16),
    "action": ((), np.int8),
}
META = "meta.json"


def storage_layout(obs_layout: Layout) -> dict:
    """name -> (row shape, stored dtype) for an encoder's observation, plus the targets."""
    out = {}
    for name, (shape, dtype) in obs_layout.items():
        if dtype == "bool":
            out[name] = (((int(np.prod(shape)) + 7) // 8,), np.uint8)
        else:
            out[name] = (tuple(shape), STORED[dtype])
    return {**out, **TARGETS}


LAYOUT = storage_layout(V1_LAYOUT)


def _empty(layout: dict, n: int) -> Dict[str, np.ndarray]:
    return {k: np.zeros((n, *shape), dtype=dt) for k, (shape, dt) in layout.items()}


class ShardWriter:
    """Thread-safe: poke-env calls `add` from its event-loop thread. Full shards
    are compressed and written by a background thread."""

    def __init__(self, root: Path | str, shard_size: int = 16384, info: Optional[dict] = None,
                 obs_layout: Optional[Layout] = None):
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)
        if (self.root / META).exists():
            raise FileExistsError(f"{self.root} already holds a dataset; pick another --data dir")
        self.shard_size = shard_size
        self.obs_layout = dict(obs_layout or V1_LAYOUT)
        self.layout = storage_layout(self.obs_layout)
        self.info = {**(info or {}), "obs_layout": {k: [list(shape), dt] for k, (shape, dt)
                                                    in self.obs_layout.items()}}
        self._lock = threading.Lock()
        self._buf = _empty(self.layout, shard_size)
        self._n = 0
        self._shards: List[dict] = []
        self._io = ThreadPoolExecutor(max_workers=1)
        self._pending = []

    @property
    def n_samples(self) -> int:
        with self._lock:
            return sum(s["n"] for s in self._shards) + self._n

    def add(self, obs: Dict[str, np.ndarray], probs: np.ndarray, action: int,
            value: float = 0.0, ret: float = 0.0) -> None:
        with self._lock:
            i = self._n
            b = self._buf
            for k, (_, dtype) in self.obs_layout.items():
                b[k][i] = np.packbits(obs[k].astype(bool).ravel()) if dtype == "bool" else obs[k]
            b["probs"][i] = probs
            b["value"][i] = value
            b["ret"][i] = ret
            b["action"][i] = action
            self._n += 1
            if self._n == self.shard_size:
                self._flush_locked()

    def _flush_locked(self) -> None:
        if self._n == 0:
            return
        data = {k: v[: self._n] for k, v in self._buf.items()}
        name = f"shard_{len(self._shards):05d}.npz"
        self._shards.append({"file": name, "n": self._n})
        self._buf, self._n = _empty(self.layout, self.shard_size), 0
        shards = list(self._shards)
        self._pending.append(self._io.submit(self._write, name, data, shards))

    def _write(self, name: str, data: Dict[str, np.ndarray], shards: List[dict]) -> None:
        tmp = self.root / (name + ".tmp")
        with open(tmp, "wb") as f:
            np.savez_compressed(f, **data)
        tmp.replace(self.root / name)
        # Meta after every shard: a crashed collection still leaves a usable dataset.
        (self.root / META).write_text(json.dumps({"shards": shards, **self.info}, indent=1))

    def close(self) -> None:
        with self._lock:
            self._flush_locked()
        for fut in self._pending:
            fut.result()
        self._io.shutdown()


def decode(rows: Dict[str, np.ndarray], obs_layout: Optional[Layout] = None) -> Dict[str, np.ndarray]:
    """Compact rows -> what the model takes (encoder.collate's dtypes), plus
    float32 targets. Teacher probabilities are renormalised after float16."""
    obs = {}
    for k, (shape, dtype) in (obs_layout or V1_LAYOUT).items():
        if dtype == "bool":
            n = int(np.prod(shape))
            obs[k] = np.unpackbits(rows[k], axis=-1, count=n).astype(bool).reshape(-1, *shape)
        else:
            obs[k] = rows[k].astype(np.dtype(dtype))
    probs = rows["probs"].astype(np.float32)
    probs /= np.maximum(probs.sum(-1, keepdims=True), 1e-8)
    return {"obs": obs, "probs": probs,
            "value": rows["value"].astype(np.float32),
            "ret": rows["ret"].astype(np.float32),
            "action": rows["action"].astype(np.int64)}


class ShardDataset:
    def __init__(self, root: Path | str, val_frac: float = 0.05, buffer_shards: int = 4):
        self.root = Path(root)
        self.meta = json.loads((self.root / META).read_text())
        self.shards: List[dict] = self.meta["shards"]
        # Datasets written before the layout was recorded hold V1 observations
        saved = self.meta.get("obs_layout")
        self.obs_layout: Layout = ({k: (tuple(shape), dt) for k, (shape, dt) in saved.items()}
                                   if saved else dict(V1_LAYOUT))
        self.layout = storage_layout(self.obs_layout)
        self.val_frac = val_frac
        self.buffer_shards = buffer_shards

    def _n_val(self, n: int) -> int:
        return max(1, int(round(n * self.val_frac))) if n > 1 and self.val_frac > 0 else 0

    def __len__(self) -> int:
        return sum(s["n"] for s in self.shards)

    def n_samples(self, split: str) -> int:
        n_val = sum(self._n_val(s["n"]) for s in self.shards)
        return n_val if split == "val" else len(self) - n_val

    def bytes_on_disk(self) -> int:
        return sum((self.root / s["file"]).stat().st_size for s in self.shards)

    def _load(self, shard: dict, split: str) -> Dict[str, np.ndarray]:
        with np.load(self.root / shard["file"]) as z:
            rows = {k: z[k] for k in self.layout}
        cut = shard["n"] - self._n_val(shard["n"])
        sl = slice(cut, None) if split == "val" else slice(0, cut)
        return {k: v[sl] for k, v in rows.items()}

    def batches(self, split: str, batch_size: int,
                rng: Optional[np.random.Generator] = None) -> Iterator[Dict[str, np.ndarray]]:
        """One pass over a split. With `rng`, shard order and rows inside each
        buffer of `buffer_shards` shards are shuffled; without, rows come in order."""
        order = np.arange(len(self.shards))
        if rng is not None:
            rng.shuffle(order)
        for g in range(0, len(order), self.buffer_shards):
            parts = [self._load(self.shards[i], split) for i in order[g: g + self.buffer_shards]]
            rows = {k: np.concatenate([p[k] for p in parts]) for k in self.layout}
            n = len(rows["action"])
            idx = rng.permutation(n) if rng is not None else np.arange(n)
            for s in range(0, n, batch_size):
                take = idx[s: s + batch_size]
                yield decode({k: v[take] for k, v in rows.items()}, self.obs_layout)


def discounted_returns(rewards: List[float], gamma: float) -> np.ndarray:
    out = np.zeros(len(rewards), dtype=np.float32)
    acc = 0.0
    for t in reversed(range(len(rewards))):
        acc = rewards[t] + gamma * acc
        out[t] = acc
    return out
