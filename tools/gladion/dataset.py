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

A dataset may also hold `extra` per-sample fields (meta.json "extra_targets": name ->
[shape, dtype]), e.g. the trajectory fields of the human replay dataset (human_data.py:
trajectory id, step, reward, done, player rating, turn), which offline RL needs to put
the rows back in trajectory order. Datasets without them read as before.
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


ExtraLayout = Dict[str, tuple]   # name -> (row shape, numpy dtype name)


def storage_layout(obs_layout: Layout, extra: Optional[ExtraLayout] = None) -> dict:
    """name -> (row shape, stored dtype) for an encoder's observation, plus the targets
    (and the `extra` fields, stored as given)."""
    out = {}
    for name, (shape, dtype) in obs_layout.items():
        if dtype == "bool":
            out[name] = (((int(np.prod(shape)) + 7) // 8,), np.uint8)
        else:
            out[name] = (tuple(shape), STORED[dtype])
    extras = {k: (tuple(shape), np.dtype(dt).type) for k, (shape, dt) in (extra or {}).items()}
    return {**out, **TARGETS, **extras}


LAYOUT = storage_layout(V1_LAYOUT)


def _empty(layout: dict, n: int) -> Dict[str, np.ndarray]:
    return {k: np.zeros((n, *shape), dtype=dt) for k, (shape, dt) in layout.items()}


class ShardWriter:
    """Thread-safe: poke-env calls `add` from its event-loop thread. Full shards
    are compressed and written by a background thread."""

    def __init__(self, root: Path | str, shard_size: int = 16384, info: Optional[dict] = None,
                 obs_layout: Optional[Layout] = None, extra: Optional[ExtraLayout] = None):
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)
        if (self.root / META).exists():
            raise FileExistsError(f"{self.root} already holds a dataset; pick another --data dir")
        self.shard_size = shard_size
        self.obs_layout = dict(obs_layout or V1_LAYOUT)
        self.extra = dict(extra or {})
        self.layout = storage_layout(self.obs_layout, self.extra)
        self.info = {**(info or {}), "obs_layout": {k: [list(shape), dt] for k, (shape, dt)
                                                    in self.obs_layout.items()}}
        if self.extra:
            self.info["extra_targets"] = {k: [list(shape), dt] for k, (shape, dt) in self.extra.items()}
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
            value: float = 0.0, ret: float = 0.0, **extra) -> None:
        """One sample; `extra` gives this writer's extra fields (missing ones are 0)."""
        with self._lock:
            i = self._n
            b = self._buf
            for k, (_, dtype) in self.obs_layout.items():
                b[k][i] = np.packbits(obs[k].astype(bool).ravel()) if dtype == "bool" else obs[k]
            b["probs"][i] = probs
            b["value"][i] = value
            b["ret"][i] = ret
            b["action"][i] = action
            for k, v in extra.items():
                b[k][i] = v
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

    def close(self) -> List[dict]:
        """Writes what is left; returns the shards written ({"file", "n"})."""
        with self._lock:
            self._flush_locked()
        for fut in self._pending:
            fut.result()
        self._io.shutdown()
        return list(self._shards)


def write_meta(root: Path | str, shards: List[dict], info: dict, obs_layout: Layout,
               extra: Optional[ExtraLayout] = None) -> None:
    """meta.json of a dataset whose shards were written elsewhere (their "file" relative to
    `root`, e.g. by several ShardWriters in sub-folders)."""
    meta = {"shards": shards, **info,
            "obs_layout": {k: [list(shape), dt] for k, (shape, dt) in obs_layout.items()}}
    if extra:
        meta["extra_targets"] = {k: [list(shape), dt] for k, (shape, dt) in extra.items()}
    Path(root).mkdir(parents=True, exist_ok=True)
    (Path(root) / META).write_text(json.dumps(meta, indent=1))


def decode(rows: Dict[str, np.ndarray], obs_layout: Optional[Layout] = None,
           extra: Optional[ExtraLayout] = None) -> Dict[str, np.ndarray]:
    """Compact rows -> what the model takes (encoder.collate's dtypes), plus
    float32 targets (extra fields: float32, or int64 for integer ones). Teacher
    probabilities are renormalised after float16."""
    obs = {}
    for k, (shape, dtype) in (obs_layout or V1_LAYOUT).items():
        if dtype == "bool":
            n = int(np.prod(shape))
            obs[k] = np.unpackbits(rows[k], axis=-1, count=n).astype(bool).reshape(-1, *shape)
        else:
            obs[k] = rows[k].astype(np.dtype(dtype))
    probs = rows["probs"].astype(np.float32)
    probs /= np.maximum(probs.sum(-1, keepdims=True), 1e-8)
    out = {"obs": obs, "probs": probs,
           "value": rows["value"].astype(np.float32),
           "ret": rows["ret"].astype(np.float32),
           "action": rows["action"].astype(np.int64)}
    for k, (_, dt) in (extra or {}).items():
        out[k] = rows[k].astype(np.int64 if np.issubdtype(np.dtype(dt), np.integer) else np.float32)
    if "row" in rows:
        out["row"] = rows["row"]
    return out


class ShardDataset:
    def __init__(self, root: Path | str, val_frac: float = 0.05, buffer_shards: int = 4):
        self.root = Path(root)
        self.meta = json.loads((self.root / META).read_text())
        self.shards: List[dict] = self.meta["shards"]
        # Datasets written before the layout was recorded hold V1 observations
        saved = self.meta.get("obs_layout")
        self.obs_layout: Layout = ({k: (tuple(shape), dt) for k, (shape, dt) in saved.items()}
                                   if saved else dict(V1_LAYOUT))
        self.extra: ExtraLayout = {k: (tuple(shape), dt) for k, (shape, dt)
                                   in (self.meta.get("extra_targets") or {}).items()}
        self.layout = storage_layout(self.obs_layout, self.extra)
        self.val_frac = val_frac
        self.buffer_shards = buffer_shards
        starts = np.cumsum([0] + [s["n"] for s in self.shards])
        self._start = {s["file"]: int(starts[i]) for i, s in enumerate(self.shards)}

    def _n_val(self, n: int) -> int:
        return max(1, int(round(n * self.val_frac))) if n > 1 and self.val_frac > 0 else 0

    def __len__(self) -> int:
        return sum(s["n"] for s in self.shards)

    def n_samples(self, split: str) -> int:
        n_val = sum(self._n_val(s["n"]) for s in self.shards)
        return n_val if split == "val" else len(self) - n_val

    def bytes_on_disk(self) -> int:
        return sum((self.root / s["file"]).stat().st_size for s in self.shards)

    def _load(self, shard: dict, split: str, keys=None) -> Dict[str, np.ndarray]:
        with np.load(self.root / shard["file"]) as z:
            rows = {k: z[k] for k in (keys or self.layout)}
        cut = shard["n"] - self._n_val(shard["n"])
        sl = slice(cut, None) if split == "val" else slice(0, cut)
        start = self._start[shard["file"]]
        rows = {k: v[sl] for k, v in rows.items()}
        rows["row"] = np.arange(start, start + shard["n"], dtype=np.int64)[sl]
        return rows

    def batches(self, split: str, batch_size: int,
                rng: Optional[np.random.Generator] = None) -> Iterator[Dict[str, np.ndarray]]:
        """One pass over a split. With `rng`, shard order and rows inside each
        buffer of `buffer_shards` shards are shuffled; without, rows come in order.
        Each batch also has "row": the rows' global indices (shard order, as written)."""
        order = np.arange(len(self.shards))
        if rng is not None:
            rng.shuffle(order)
        keys = list(self.layout) + ["row"]
        for g in range(0, len(order), self.buffer_shards):
            parts = [self._load(self.shards[i], split) for i in order[g: g + self.buffer_shards]]
            rows = {k: np.concatenate([p[k] for p in parts]) for k in keys}
            n = len(rows["action"])
            idx = rng.permutation(n) if rng is not None else np.arange(n)
            for s in range(0, n, batch_size):
                take = idx[s: s + batch_size]
                yield decode({k: v[take] for k, v in rows.items()}, self.obs_layout, self.extra)

    def column(self, name: str, split: str = "train") -> np.ndarray:
        """One target or extra field for every row of a split, in global row order (cheap:
        no observation is decoded)."""
        parts = [self._load(s, split, keys=[name])[name] for s in self.shards]
        return np.concatenate(parts) if parts else np.zeros(0)


def discounted_returns(rewards: List[float], gamma: float) -> np.ndarray:
    out = np.zeros(len(rewards), dtype=np.float32)
    acc = 0.0
    for t in reversed(range(len(rewards))):
        acc = rewards[t] + gamma * acc
        out[t] = acc
    return out
