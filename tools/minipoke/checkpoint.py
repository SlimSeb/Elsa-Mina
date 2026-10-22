"""Full training state, so a 48 h run can stop and resume at any iteration."""

from __future__ import annotations

import os
import random
from pathlib import Path
from typing import Optional

import numpy as np
import torch

from model import MiniPokeTransformer, build_model


def rng_state() -> dict:
    state = {"python": random.getstate(), "numpy": np.random.get_state(),
             "torch": torch.get_rng_state()}
    if torch.cuda.is_available():
        state["cuda"] = torch.cuda.get_rng_state_all()
    if torch.backends.mps.is_available():
        state["mps"] = torch.mps.get_rng_state()
    return state


def set_rng_state(state: Optional[dict]) -> None:
    if not state:
        return
    random.setstate(state["python"])
    np.random.set_state(state["numpy"])
    torch.set_rng_state(state["torch"])
    if "cuda" in state and torch.cuda.is_available():
        torch.cuda.set_rng_state_all(state["cuda"])
    if "mps" in state and torch.backends.mps.is_available():
        torch.mps.set_rng_state(state["mps"])


def seed_everything(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)


def save_checkpoint(path: Path | str, model: MiniPokeTransformer,
                    optimizer: Optional[torch.optim.Optimizer] = None, **extra) -> None:
    """Atomic: written to a temporary file, then renamed, so a crash or a kill
    during the write never leaves a truncated checkpoint behind."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    state = {"model": {k: v.detach().cpu() for k, v in model.state_dict().items()},
             "cfg": model.cfg.to_dict(),
             "opt": optimizer.state_dict() if optimizer is not None else None,
             **extra}
    tmp = path.with_name(path.name + ".tmp")
    torch.save(state, tmp)
    os.replace(tmp, path)


def load_checkpoint(path: Path | str) -> dict:
    return torch.load(path, map_location="cpu", weights_only=False)


def load_model(path: Path | str, device) -> MiniPokeTransformer:
    return build_model(load_checkpoint(path)).to(device)

