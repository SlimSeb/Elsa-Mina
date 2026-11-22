"""Checkpoints: the weights, the model config, the optimizer and the set priors."""

from __future__ import annotations

import os
import random
from pathlib import Path
from typing import Optional

import numpy as np
import torch

from model import MiniPokeTransformer, build_model


def seed_everything(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)


def model_extras(model: MiniPokeTransformer) -> dict:
    """What a checkpoint needs beyond the weights to rebuild the model's observations:
    an encoder v2 model's set priors (Showdown's sets change over time)."""
    priors = getattr(model, "priors", None)
    return {"set_priors": priors.to_dict()} if priors is not None else {}


def save_checkpoint(path: Path | str, model: MiniPokeTransformer,
                    optimizer: Optional[torch.optim.Optimizer] = None, **extra) -> None:
    """Atomic: written to a temporary file, then renamed, so a crash or a kill
    during the write never leaves a truncated checkpoint behind."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    state = {"model": {k: v.detach().cpu() for k, v in model.state_dict().items()},
             "cfg": model.cfg.to_dict(),
             "opt": optimizer.state_dict() if optimizer is not None else None,
             **model_extras(model), **extra}
    tmp = path.with_name(path.name + ".tmp")
    torch.save(state, tmp)
    os.replace(tmp, path)


def load_checkpoint(path: Path | str) -> dict:
    return torch.load(path, map_location="cpu", weights_only=False)


def load_model(path: Path | str, device) -> MiniPokeTransformer:
    return build_model(load_checkpoint(path)).to(device)

