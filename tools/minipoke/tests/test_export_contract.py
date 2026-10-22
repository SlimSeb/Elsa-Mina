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


# --------------------------------------------------------------------------
# minipoke-v2 (encoder_version 2): its own inputs and spec, same outputs
# --------------------------------------------------------------------------
V2_MODELS = {
    "v2-small": dict(size="small", encoder_version=2),
    "v2-medium": dict(size="medium", encoder_version=2),
    "v2-move-tokens": dict(size="small", encoder_version=2, move_tokens=True),
    "v2-history": dict(size="small", encoder_version=2, history=True),
    "v2-typed-ffn": dict(size="small", encoder_version=2, move_tokens=True, typed_ffn=True),
    "v2-history-tokens": dict(size="small", encoder_version=2, history_tokens=8, typed_ffn=True),
}


@pytest.fixture(scope="module", params=list(V2_MODELS))
def exported_v2(request, tmp_path_factory):
    from tests.helpers import make_model
    cfg = dict(V2_MODELS[request.param])
    model = make_model(cfg.pop("size"), **cfg).eval()
    out = tmp_path_factory.mktemp(request.param)
    return model, export(model, out), out


def test_v2_export_takes_v1s_inputs_then_its_own(exported_v2):
    from encoder_v2 import model_encoder
    model, onnx_path, _ = exported_v2
    graph = onnx.load(str(onnx_path)).graph
    got = signature(graph.input)
    assert got[:6] == CONTRACT_INPUTS
    dtypes = {"int64": I64, "float32": F, "bool": B}
    assert got[6:] == [(n, dtypes[dt], ["batch", *shape])
                       for n, (shape, dt) in list(model_encoder(model).layout.items())[6:]]
    assert [n for n, *_ in got[6:11]] == ["cand_ids", "cand_feats", "prior_ids", "prior_w",
                                          "prior_feats"]
    extra = [n for n, *_ in got[11:]]
    assert ("hist_ids" in extra) == (model.cfg.history and not model.cfg.history_tokens)
    assert ("event_ids" in extra) == bool(model.cfg.history_tokens)
    assert signature(graph.output) == CONTRACT_OUTPUTS


def test_v2_export_matches_pytorch(exported_v2):
    model, onnx_path, _ = exported_v2
    diff = parity(model, onnx_path, batch=37)
    assert diff["logits"] < 1e-4 and diff["value"] < 1e-5


def test_v2_spec_documents_the_encoder_and_ships_the_priors(exported_v2):
    import hashlib
    import json
    model, _, out = exported_v2
    spec = json.loads((out / "encoder_spec.json").read_text())
    v1 = json.loads((DEPLOYED_V1 / "encoder_spec.json").read_text())
    assert spec["encoder_version"] == 2
    assert {k: spec[k] for k in v1 if k != "orderings"} == {k: v for k, v in v1.items()
                                                            if k != "orderings"}
    assert all(spec["orderings"][k] == v for k, v in v1["orderings"].items())
    assert [i["name"] for i in spec["inputs"]][:6] == [n for n, *_ in CONTRACT_INPUTS]
    priors = spec["set_priors"]
    assert priors["candidate_moves_k"] == 8 and "ties by move id" in priors["candidate_ranking"]
    data = (out / "set_priors.json").read_bytes()
    assert hashlib.sha256(data).hexdigest() == priors["sha256"] == model.priors.sha256
    assert priors["source_sha256"] == model.priors.source_sha256
    assert spec["model"]["history"] == model.cfg.history
    assert ("history" in spec) == model.cfg.history
    if model.cfg.history:  # what the C# tracker needs on top of poke-env's state
        assert "_replay_data" in spec["history"]["protocol_log"]
        assert len(spec["orderings"]["hist_feats"]) == 8


def test_v2_checkpoints_round_trip_with_their_priors(tmp_path):
    from checkpoint import load_checkpoint, save_checkpoint
    from export_onnx import export_checkpoint
    from tests.helpers import make_model
    model = make_model("small", encoder_version=2)
    save_checkpoint(tmp_path / "v2.pt", model)
    again = build_model(load_checkpoint(tmp_path / "v2.pt"))
    assert again.cfg.encoder_version == 2 and again.priors.sha256 == model.priors.sha256
    diff = export_checkpoint(tmp_path / "v2.pt", tmp_path / "export")
    assert diff["logits"] < 1e-4
    assert (tmp_path / "export" / "set_priors.json").exists()


def test_v1_move_token_model_keeps_v1s_signature_and_matches_pytorch(tmp_path):
    from tests.helpers import make_model
    model = make_model("small", move_tokens=True).eval()
    onnx_path = export(model, tmp_path)
    graph = onnx.load(str(onnx_path)).graph
    assert signature(graph.input) == CONTRACT_INPUTS
    assert signature(graph.output) == CONTRACT_OUTPUTS
    diff = parity(model, onnx_path, batch=19)
    assert diff["logits"] < 1e-4 and diff["value"] < 1e-5
