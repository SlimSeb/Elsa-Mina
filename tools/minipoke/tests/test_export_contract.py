"""The deployed interface: ElsaMina reimplements encoder.py and feeds minipoke.onnx.

Every model size must export to the same graph signature, and the encoder spec
must not change by a single byte (the C# encoder is a port of it).
"""

from pathlib import Path

import onnx
import pytest
import torch

import encoder as E
from export_onnx import export, parity
from model import PRESETS, MiniPokeTransformer, ModelConfig, build_model

# A released V1 model: its encoder_spec.json is the one ElsaMina's C# encoder ports
DEPLOYED_V1 = Path(__file__).resolve().parents[3] / "src/ElsaMina.Battles/Models/MiniPokeV1_4M"

F, I64, B = onnx.TensorProto.FLOAT, onnx.TensorProto.INT64, onnx.TensorProto.BOOL
CONTRACT_INPUTS = [
    ("mon_ids", I64, ["batch", E.N_MONS, 3]),
    ("move_ids", I64, ["batch", E.N_MONS, E.N_MOVES]),
    ("mon_feats", F, ["batch", E.N_MONS, 84]),
    ("move_feats", F, ["batch", E.N_MONS, E.N_MOVES, 39]),
    ("field", F, ["batch", 81]),
    ("mask", B, ["batch", 14]),
]
CONTRACT_OUTPUTS = [("logits", F, ["batch", 14]), ("value", F, ["batch"])]


def signature(values):
    return [(v.name, v.type.tensor_type.elem_type,
             [d.dim_param or d.dim_value for d in v.type.tensor_type.shape.dim]) for v in values]


@pytest.fixture(scope="module", params=list(PRESETS))
def exported(request, tmp_path_factory):
    torch.manual_seed(0)
    model = MiniPokeTransformer(PRESETS[request.param]).eval()
    out = tmp_path_factory.mktemp(request.param)
    return model, export(model, out), out


def test_export_has_the_deployed_signature(exported):
    _, onnx_path, _ = exported
    graph = onnx.load(str(onnx_path)).graph
    assert signature(graph.input) == CONTRACT_INPUTS
    assert signature(graph.output) == CONTRACT_OUTPUTS


@pytest.mark.skipif(not DEPLOYED_V1.exists(), reason="ElsaMina's MiniPokeV1_4M model not found")
def test_export_matches_the_deployed_v1_graph_signature(exported):
    _, onnx_path, _ = exported
    deployed, ours = onnx.load(str(DEPLOYED_V1 / "minipoke.onnx")).graph, onnx.load(str(onnx_path)).graph
    assert signature(ours.input) == signature(deployed.input)
    assert signature(ours.output) == signature(deployed.output)


def test_export_runs_any_batch_size_and_matches_pytorch(exported):
    model, onnx_path, _ = exported
    diff = parity(model, onnx_path, batch=37)
    assert diff["logits"] < 1e-4 and diff["value"] < 1e-5


def test_encoder_spec_is_byte_identical_to_the_reference(exported):
    _, _, out = exported
    assert (out / "encoder_spec.json").read_bytes() == (DEPLOYED_V1 / "encoder_spec.json").read_bytes()


def test_encoder_layout_is_unchanged():
    assert (E.N_MONS, E.N_MOVES, E.F_MON, E.F_MOVE, E.F_FIELD, E.ACT_DIM) == (12, 4, 84, 39, 81, 14)


def test_value_output_is_the_expectation_of_the_distributional_head():
    torch.manual_seed(0)
    model = MiniPokeTransformer(PRESETS["medium"]).eval()
    from export_onnx import random_inputs, INPUTS
    obs = dict(zip(INPUTS, random_inputs(5)))
    with torch.no_grad():
        _, value, value_logits = model.forward_train(obs)
    expected = (torch.softmax(value_logits, -1) * model.value_support).sum(-1)
    assert value.shape == (5,)
    torch.testing.assert_close(value, expected)


def test_v1_checkpoints_without_the_new_config_fields_still_load():
    old_cfg = {"d_model": 128, "n_layers": 3, "n_heads": 4, "ff_mult": 2.0, "dropout": 0.0,
               "species_dim": 32, "item_dim": 16, "ability_dim": 16, "move_dim": 32}
    old_state = MiniPokeTransformer(ModelConfig(**old_cfg)).state_dict()
    model = build_model({"cfg": old_cfg, "model": old_state})
    assert model.cfg == PRESETS["small"]
    assert not model.distributional


def test_medium_preset_is_in_the_target_size():
    n = sum(p.numel() for p in MiniPokeTransformer(PRESETS["medium"]).parameters())
    assert 3.5e6 <= n <= 4.5e6


def test_size_is_recognised_from_a_checkpoint_config():
    from dataclasses import replace
    from model import size_of
    assert size_of(PRESETS["medium"]) == "medium"
    assert size_of(replace(PRESETS["medium"], value_bins=1, dropout=0.05)) == "medium"
    assert size_of(ModelConfig.from_dict({"d_model": 128, "n_layers": 3})) == "small"
