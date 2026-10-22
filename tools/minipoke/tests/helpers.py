"""Synthetic observations and episodes (no Showdown needed)."""

import numpy as np

from export_onnx import INPUTS, random_inputs


def random_obs(n: int, seed: int = 0):
    """n single observations in encoder.encode's format."""
    tensors = random_inputs(n, seed)
    batch = {k: t.numpy() for k, t in zip(INPUTS, tensors)}
    return [{k: v[i] for k, v in batch.items()} for i in range(n)]


def random_episodes(n_episodes: int, length: int, seed: int = 0):
    rng = np.random.default_rng(seed)
    obs = random_obs(n_episodes * length, seed)
    episodes = []
    for e in range(n_episodes):
        ep = []
        for t in range(length):
            o = obs[e * length + t]
            legal = np.flatnonzero(o["mask"])
            ep.append({"obs": o, "action": int(rng.choice(legal)), "logp": float(-rng.random() * 2),
                       "value": float(rng.uniform(-1, 1)),
                       "reward": float(rng.choice([0.0, 0.1, -0.1]))})
        ep[-1]["reward"] += float(rng.choice([1.0, -1.0]))
        episodes.append(ep)
    return episodes
