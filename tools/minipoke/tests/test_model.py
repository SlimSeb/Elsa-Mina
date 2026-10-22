"""Model behaviour shared by every architecture flag."""

import pytest
import torch

from encoder import ACT_DIM, F_MOVE
from export_onnx import model_inputs, random_inputs
from tests.helpers import make_model

CONFIGS = {
    "v1": dict(),
    "v2": dict(encoder_version=2),
}


def batch_for(model, n=6, seed=0):
    return dict(zip(model_inputs(model), random_inputs(n, seed, model=model)))


@pytest.mark.parametrize("name", list(CONFIGS))
def test_illegal_actions_are_masked(name):
    model = make_model("small", **CONFIGS[name]).eval()
    obs = batch_for(model)
    with torch.no_grad():
        logits, value = model(obs)
    assert logits.shape == (6, ACT_DIM) and value.shape == (6,)
    assert (logits[~obs["mask"]] <= -1e8).all()
    assert torch.isfinite(logits[obs["mask"]]).all() and (logits[obs["mask"]] > -1e8).all()
    probs = torch.softmax(logits, -1)
    assert probs[~obs["mask"]].max() == 0


def test_v2_reads_the_opponents_candidate_moves():
    model = make_model("small", encoder_version=2).eval()
    obs = batch_for(model)
    other = {k: v.clone() for k, v in obs.items()}
    other["cand_feats"][:, :, :, F_MOVE] = 1.0 - other["cand_feats"][:, :, :, F_MOVE]
    with torch.no_grad():
        a, b = model(obs)[0], model(other)[0]
    assert not torch.allclose(a[obs["mask"]], b[obs["mask"]])
