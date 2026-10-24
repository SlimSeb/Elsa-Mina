"""
Export a trained checkpoint for use outside Python (e.g. ElsaMina in C#).

    python export_onnx.py --ckpt checkpoints/ppo_latest.pt --out export/

Writes:
    export/minipoke.onnx        the network (inputs below, outputs: logits [B,14], value [B])
    export/encoder_spec.json    everything a non-Python encoder must reproduce exactly:
                                vocabularies, item hashing, one-hot orderings, feature sizes,
                                action layout
    export/set_priors.json      encoder v2 only: the random battle set priors it was
                                trained with (normalized, see setpriors.py)

A V1 model's encoder_spec.json is byte-identical to the released V1 one. A v2
spec adds "encoder_version": 2, the ONNX "inputs" (V1's six, then v2's), the
rules of every v2 array and the model's flags.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
import torch
import torch.nn as nn

import encoder as E
from encoder_v2 import ENCODER_FLAGS, model_encoder, random_obs
from model import MiniPokeTransformer, build_model, describe

INPUTS = ["mon_ids", "move_ids", "mon_feats", "move_feats", "field", "mask"]
OUTPUTS = ["logits", "value"]
OPSET = 17


class OnnxWrapper(nn.Module):
    def __init__(self, model: MiniPokeTransformer, names=None):
        super().__init__()
        self.model = model
        self.names = list(names or INPUTS)

    def forward(self, *inputs):
        return self.model(dict(zip(self.names, inputs)))


def dummy_inputs(batch: int = 2):
    return (
        torch.ones(batch, E.N_MONS, 3, dtype=torch.long),
        torch.ones(batch, E.N_MONS, E.N_MOVES, dtype=torch.long),
        torch.zeros(batch, E.N_MONS, E.F_MON),
        torch.zeros(batch, E.N_MONS, E.N_MOVES, E.F_MOVE),
        torch.zeros(batch, E.F_FIELD),
        torch.ones(batch, E.ACT_DIM, dtype=torch.bool),
    )


def model_inputs(model: MiniPokeTransformer):
    return model_encoder(model).input_names


def random_inputs(batch: int, seed: int = 0, model: MiniPokeTransformer = None):
    """Plausible observations (ids in range, one active Pokémon, some legal
    actions) so the parity check exercises every branch of the network. V1's six
    arrays, then (for a v2 model) its other inputs, in model_inputs() order."""
    v1 = _random_v1(batch, seed)
    if model is None or model.cfg.encoder_version == 1:
        return v1
    extra = random_obs(model_encoder(model).layout, batch, seed)
    return v1 + tuple(torch.from_numpy(extra[n]) for n in model_inputs(model)[len(INPUTS):])


def _random_v1(batch: int, seed: int):
    g = torch.Generator().manual_seed(seed)
    mon_ids = torch.stack([torch.randint(0, E.VOCAB_SIZES[k], (batch, E.N_MONS), generator=g)
                           for k in ("species", "item", "ability")], dim=-1)
    move_ids = torch.randint(0, E.VOCAB_SIZES["move"], (batch, E.N_MONS, E.N_MOVES), generator=g)
    mon_feats = torch.rand(batch, E.N_MONS, E.F_MON, generator=g)
    mon_feats[..., 3] = 0.0
    mon_feats[torch.arange(batch), torch.randint(0, E.N_SLOTS, (batch,), generator=g), 3] = 1.0
    move_feats = torch.rand(batch, E.N_MONS, E.N_MOVES, E.F_MOVE, generator=g)
    move_feats[..., 0] = (move_feats[..., 0] > 0.2).float()
    field = torch.rand(batch, E.F_FIELD, generator=g)
    mask = torch.rand(batch, E.ACT_DIM, generator=g) > 0.5
    mask[:, 6] = True
    return mon_ids, move_ids, mon_feats, move_feats, field, mask


def write_spec(path: Path, model: MiniPokeTransformer = None) -> None:
    names = lambda xs: [x.name for x in xs]
    spec = {
        "n_mons": E.N_MONS, "n_slots": E.N_SLOTS, "n_moves": E.N_MOVES, "act_dim": E.ACT_DIM,
        "f_mon": E.F_MON, "f_move": E.F_MOVE, "f_field": E.F_FIELD,
        "pad_id": E.PAD_ID, "unk_id": E.UNK_ID,
        "item_hash": {"algorithm": "crc32(utf8(item_id)) % buckets + 2",
                      "buckets": E.N_ITEM_BUCKETS},
        "vocab": {"species": E.SPECIES_VOCAB, "move": E.MOVE_VOCAB, "ability": E.ABILITY_VOCAB},
        "orderings": {
            "types": names(E.TYPES), "statuses": names(E.STATUSES), "boosts": E.BOOSTS,
            "stats": E.STATS, "effects": names(E.EFFECTS), "weathers": names(E.WEATHERS),
            "fields": names(E.FIELDS), "side_conditions": names(E.SIDE_CONDS),
            "categories": names(E.CATEGORIES),
        },
        "actions": {"0-5": "switch to team slot i (stable first-seen team order)",
                    "6-9": "move j (order of the active Pokémon's moves)",
                    "10-13": "terastallize + move j"},
    }
    if model is not None and model.cfg.encoder_version > 1:
        # Only v2+ specs carry the version: V1's file stays byte-identical to the released one
        encoder = model_encoder(model)
        extra = encoder.spec()
        spec = {"encoder_version": encoder.version, **spec,
                "inputs": [{"name": n, "shape": ["batch", *shape], "dtype": dt}
                           for n, (shape, dt) in encoder.layout.items()],
                "model": {k: v for k, v in model.cfg.to_dict().items()
                          if k in ENCODER_FLAGS},
                **{k: v for k, v in extra.items() if k != "orderings"}}
        spec["orderings"].update(extra.get("orderings", {}))
    # "\n" on every OS: ElsaMina's copy must stay byte-identical (write_text would emit CRLF on Windows)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(spec, indent=1))


def export(model: MiniPokeTransformer, out_dir: Path | str) -> Path:
    """Writes minipoke.onnx and encoder_spec.json into out_dir. The graph has the
    same inputs and outputs for every model size (see tests/test_export_contract.py)."""
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    onnx_path = out / "minipoke.onnx"
    model = model.to("cpu").eval()
    names = model_inputs(model)
    v1 = model.cfg.encoder_version == 1
    torch.onnx.export(
        OnnxWrapper(model, names), dummy_inputs() if v1 else random_inputs(2, model=model),
        str(onnx_path), input_names=names, output_names=OUTPUTS,
        dynamic_axes={n: {0: "batch"} for n in names + OUTPUTS},
        opset_version=OPSET, dynamo=False,
    )
    write_spec(out / "encoder_spec.json", model)
    if model.priors is not None:
        with open(out / "set_priors.json", "w", encoding="utf-8", newline="\n") as f:
            f.write(model.priors.to_json())
    return onnx_path


def parity(model: MiniPokeTransformer, onnx_path: Path | str, batch: int = 64) -> dict:
    """Max |ONNX - PyTorch| on random observations, or {} without onnxruntime."""
    try:
        import onnxruntime as ort
    except ImportError:
        return {}
    x = random_inputs(batch, model=model)
    names = model_inputs(model)
    with torch.no_grad():
        ref_logits, ref_v = OnnxWrapper(model.to("cpu").eval(), names)(*x)
    sess = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    got_logits, got_v = sess.run(None, {n: t.numpy() for n, t in zip(names, x)})
    legal = x[5].numpy()
    return {"logits": float(np.abs(got_logits - ref_logits.numpy())[legal].max()),
            "value": float(np.abs(got_v - ref_v.numpy()).max())}


def export_checkpoint(ckpt: Path | str, out_dir: Path | str) -> dict:
    state = torch.load(ckpt, map_location="cpu", weights_only=False)
    model = build_model(state)
    onnx_path = export(model, out_dir)
    diff = parity(model, onnx_path)
    print(f"[export] {ckpt} ({describe(model)}) -> {onnx_path}")
    if diff:
        print(f"[export] parity vs PyTorch: max |logits diff| {diff['logits']:.2e}, "
              f"max |value diff| {diff['value']:.2e}")
    else:
        print("[export] (pip install onnxruntime to run the parity check)")
    return diff


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ckpt", required=True)
    ap.add_argument("--out", default="export")
    args = ap.parse_args()
    export_checkpoint(args.ckpt, args.out)


if __name__ == "__main__":
    main()
