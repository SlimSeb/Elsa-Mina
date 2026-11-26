"""Model behaviour shared by every architecture flag."""

import pytest
import torch

from encoder import ACT_DIM, F_MOVE, N_MONS, N_MOVES, N_SLOTS
from export_onnx import model_inputs, random_inputs
from model import ACTOR, CRITIC, FIELD_TOK, MOVE0, N_TOKENS
from tests.helpers import make_model

CONFIGS = {
    "v1": dict(),
    "v2": dict(encoder_version=2),
    "v1-move-tokens": dict(move_tokens=True),
    "v2-move-tokens": dict(encoder_version=2, move_tokens=True),
    "v2-history": dict(encoder_version=2, history=True),
    "v1-typed-ffn": dict(typed_ffn=True),
    "v2-move-tokens-typed-ffn": dict(encoder_version=2, move_tokens=True, typed_ffn=True),
    "v2-history-tokens": dict(encoder_version=2, history_tokens=8),
    "v2-matchups": dict(encoder_version=2, matchup_tokens=True),
    "v2.1-meanmax": dict(encoder_version=2, history=True, move_effects=True, move_pool="meanmax"),
    "v2-everything": dict(encoder_version=2, move_tokens=True, history_tokens=8,
                          matchup_tokens=True, typed_ffn=True),
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


# --------------------------------------------------------------------------
# Move tokens
# --------------------------------------------------------------------------
@pytest.mark.parametrize("v2", [False, True])
def test_attention_mask_with_move_tokens(v2):
    model = make_model("small", encoder_version=2 if v2 else 1, move_tokens=True, cand_moves=8)
    n_moves = N_MONS * N_MOVES + (N_SLOTS * 8 if v2 else 0)
    T = N_TOKENS + n_moves
    assert model.n_tokens == T
    obs = batch_for(model, n=3)
    obs["move_feats"][0, 2, 3, 0] = 0.0          # sample 0: our mon 2 has no 4th move
    x, present, _ = model.embed(obs)
    assert x.shape == (3, T, model.cfg.d_model) and present.shape == (3, T)
    mask = model.attention_mask(present)
    assert mask.shape == (3, 1, T, T) and mask.dtype == torch.bool
    m = mask[0, 0]
    state = list(range(FIELD_TOK, T))
    empty = MOVE0 + 2 * N_MOVES + 3
    assert not present[0, empty] and present[1, empty] == (obs["move_feats"][1, 2, 3, 0] > 0)
    keys = {k for k in state if present[0, k]}
    for q in [ACTOR, CRITIC] + state:
        # actor / critic: the present state and themselves; state: the present state,
        # and itself even when empty
        assert set(torch.nonzero(m[q]).flatten().tolist()) == keys | {q}
    assert m[:, ACTOR].tolist() == [q == ACTOR for q in range(T)]   # nobody reads the actor
    assert m[:, CRITIC].tolist() == [q == CRITIC for q in range(T)]
    assert m.any(dim=-1).all()                      # no empty row (no NaN in SDPA)
    if v2:  # candidate tokens follow the 48 known ones
        first_cand = MOVE0 + N_MONS * N_MOVES
        assert present[:, first_cand:].tolist() == (obs["cand_feats"][..., 0].reshape(3, -1) > 0).tolist()


@pytest.mark.parametrize("v2", [False, True])
def test_move_head_reads_the_contextualized_move_tokens(v2):
    model = make_model("small", encoder_version=2 if v2 else 1, move_tokens=True).eval()
    obs = batch_for(model, n=4)
    obs["mask"][:, 6:10] = True
    captured = {}
    model.move_head.register_forward_hook(lambda mod, inp, out: captured.update(x=inp[0]))
    final = {}
    model.final_norm.register_forward_hook(lambda mod, inp, out: final.update(x=out))
    with torch.no_grad():
        logits, _ = model(obs)
    d = model.cfg.d_model
    active = obs["mon_feats"][:, :N_SLOTS, 3].argmax(-1)
    for b in range(4):
        pos = MOVE0 + int(active[b]) * N_MOVES + torch.arange(N_MOVES)
        torch.testing.assert_close(captured["x"][b, :, 2 * d:], final["x"][b, pos])

    # Only another token changes (an opponent's species): our move logits change
    other = {k: v.clone() for k, v in obs.items()}
    other["mon_ids"][:, N_SLOTS + 1, 0] = (other["mon_ids"][:, N_SLOTS + 1, 0] + 7) % 1000 + 2
    with torch.no_grad():
        changed, _ = model(other)
    assert not torch.allclose(logits[:, 6:10], changed[:, 6:10])


def test_without_move_tokens_the_move_head_reads_the_move_vectors():
    model = make_model("small").eval()
    obs = batch_for(model, n=2)
    captured = {}
    model.move_head.register_forward_hook(lambda mod, inp, out: captured.update(x=inp[0]))
    with torch.no_grad():
        _, _, move_vec = model.embed(obs)
        model(obs)
    active = obs["mon_feats"][:, :N_SLOTS, 3].argmax(-1)
    d = model.cfg.d_model
    for b in range(2):
        torch.testing.assert_close(captured["x"][b, :, 2 * d:], move_vec[b, active[b]])


# --------------------------------------------------------------------------
# Devices
# --------------------------------------------------------------------------
DEVICES = ["cpu"] + (["cuda"] if torch.cuda.is_available() else [])


@pytest.mark.skipif(not torch.cuda.is_available(), reason="no GPU")
@pytest.mark.parametrize("name", list(CONFIGS))
def test_forward_and_backward_on_cuda(name):
    model = make_model("small", **CONFIGS[name]).to("cuda").train()
    obs = {k: v.to("cuda") for k, v in batch_for(model, n=8).items()}
    logits, value, _ = model.forward_train(obs)
    (torch.log_softmax(logits, -1).mean() + value.mean()).backward()
    assert all(torch.isfinite(p.grad).all() for p in model.parameters() if p.grad is not None)
    with torch.no_grad():
        cpu_logits, _ = make_model("small", **CONFIGS[name]).eval()(
            {k: v.cpu() for k, v in obs.items()})
    legal = obs["mask"].cpu()
    torch.testing.assert_close(logits.detach().cpu()[legal], cpu_logits[legal], atol=1e-3, rtol=1e-3)


@pytest.mark.parametrize("device", DEVICES)
@pytest.mark.parametrize("name", list(CONFIGS))
def test_bf16_autocast_forward_stays_close_to_float32(name, device):
    """train.py offline --amp bf16: the training forward pass in bfloat16 autocast."""
    model = make_model("small", **CONFIGS[name]).to(device).train()
    obs = {k: v.to(device) for k, v in batch_for(model, n=8).items()}
    with torch.autocast(device, dtype=torch.bfloat16):
        logits, value, _ = model.forward_train(obs)
    logits, value = logits.float(), value.float()
    (torch.log_softmax(logits, -1).mean() + value.mean()).backward()
    assert all(p.grad.dtype == torch.float32 and torch.isfinite(p.grad).all()
               for p in model.parameters() if p.grad is not None)
    with torch.no_grad():
        ref_logits, ref_value = model(obs)
    legal = obs["mask"]
    assert (logits[~legal] <= -1e8).all()
    torch.testing.assert_close(logits.detach()[legal], ref_logits[legal], atol=5e-2, rtol=5e-2)
    torch.testing.assert_close(value.detach(), ref_value, atol=5e-2, rtol=5e-2)


def test_pick_device_takes_auto_cuda_or_cpu():
    from model import pick_device
    assert pick_device("auto").type == ("cuda" if torch.cuda.is_available() else "cpu")
    assert pick_device("cpu") == torch.device("cpu")
    with pytest.raises(ValueError, match="auto, cuda, cuda:N or cpu"):
        pick_device("mps")
    if not torch.cuda.is_available():
        with pytest.raises(ValueError, match="sees no GPU"):
            pick_device("cuda")


@pytest.mark.parametrize("device", DEVICES)
def test_to_tensors_keeps_dtypes_and_values(device):
    import numpy as np
    from model import to_tensors
    batch = {"ids": np.arange(6, dtype=np.int64).reshape(2, 3), "mask": np.array([True, False]),
             "feats": np.linspace(0, 1, 4)}                          # float64 -> float32
    out = to_tensors(batch, device)
    assert {k: v.dtype for k, v in out.items()} == {"ids": torch.int64, "mask": torch.bool,
                                                    "feats": torch.float32}
    assert all(v.device.type == device for v in out.values())
    assert out["ids"].cpu().tolist() == [[0, 1, 2], [3, 4, 5]] and out["mask"].cpu().tolist() == [True, False]


# --------------------------------------------------------------------------
# Typed FFN
# --------------------------------------------------------------------------
def test_typed_ffn_applies_each_types_block_to_its_tokens():
    model = make_model("small", move_tokens=True, typed_ffn=True).eval()
    assert [t for t, _, _ in model.segments] == ["cls", "field", "mons", "moves"]
    assert model.segments[-1][2] == model.n_tokens
    assert all(a[2] == b[1] for a, b in zip(model.segments, model.segments[1:]))  # contiguous
    layer = model.layers[0]
    assert set(layer.ffs) == {"cls", "field", "mons", "moves"} and not hasattr(layer, "ff")
    h = torch.randn(2, model.n_tokens, model.cfg.d_model)
    out = layer.feed_forward(h, model.segments)
    for t, start, end in model.segments:
        torch.testing.assert_close(out[:, start:end], layer.ffs[t](h[:, start:end]))
    # V1 layers keep their single block (and their checkpoint keys)
    assert hasattr(make_model("small").layers[0], "ff")
    assert "layers.0.ff.0.weight" in make_model("small").state_dict()
