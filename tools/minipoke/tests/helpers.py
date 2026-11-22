"""Synthetic observations and models (no Showdown needed)."""

from export_onnx import INPUTS, random_inputs


def random_obs(n: int, seed: int = 0):
    """n single observations in encoder.encode's format."""
    tensors = random_inputs(n, seed)
    batch = {k: t.numpy() for k, t in zip(INPUTS, tensors)}
    return [{k: v[i] for k, v in batch.items()} for i in range(n)]


def make_model(size: str = "small", seed: int = 0, **cfg):
    """A preset model with config overrides; an encoder v2 one gets the hand-built priors."""
    import torch
    from model import MiniPokeTransformer, preset
    from tests.priors import hand_priors
    torch.manual_seed(seed)
    model = MiniPokeTransformer(preset(size, **cfg))
    if model.cfg.encoder_version >= 2:
        model.priors = hand_priors()
    return model


def model_obs(model, n: int, seed: int = 0):
    """n single observations in the model's encoder format."""
    from export_onnx import model_inputs, random_inputs
    tensors = random_inputs(n, seed, model=model)
    batch = {k: t.numpy() for k, t in zip(model_inputs(model), tensors)}
    return [{k: v[i] for k, v in batch.items()} for i in range(n)]
