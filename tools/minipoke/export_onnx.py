"""
Export a trained checkpoint for use outside Python (e.g. ElsaMina in C#).

    python export_onnx.py --ckpt checkpoints/ppo_latest.pt --out export/

Writes:
    export/minipoke.onnx        the network (inputs below, outputs: logits [B,14], value [B])
    export/encoder_spec.json    everything a non-Python encoder must reproduce exactly:
                                vocabularies, item hashing, one-hot orderings, feature sizes,
                                action layout
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
import torch
import torch.nn as nn

import encoder as E
from model import MiniPokeTransformer, ModelConfig

INPUTS = ["mon_ids", "move_ids", "mon_feats", "move_feats", "field", "mask"]


class OnnxWrapper(nn.Module):
    def __init__(self, model: MiniPokeTransformer):
        super().__init__()
        self.model = model

    def forward(self, mon_ids, move_ids, mon_feats, move_feats, field, mask):
        return self.model({"mon_ids": mon_ids, "move_ids": move_ids, "mon_feats": mon_feats,
                           "move_feats": move_feats, "field": field, "mask": mask})


def dummy_inputs(batch: int = 2):
    return (
        torch.ones(batch, E.N_MONS, 3, dtype=torch.long),
        torch.ones(batch, E.N_MONS, E.N_MOVES, dtype=torch.long),
        torch.zeros(batch, E.N_MONS, E.F_MON),
        torch.zeros(batch, E.N_MONS, E.N_MOVES, E.F_MOVE),
        torch.zeros(batch, E.F_FIELD),
        torch.ones(batch, E.ACT_DIM, dtype=torch.bool),
    )


def write_spec(path: Path) -> None:
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
    path.write_text(json.dumps(spec, indent=1))


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ckpt", required=True)
    ap.add_argument("--out", default="export")
    args = ap.parse_args()

    ck = torch.load(args.ckpt, map_location="cpu")
    model = MiniPokeTransformer(ModelConfig(**ck["cfg"]))
    model.load_state_dict(ck["model"])
    wrapper = OnnxWrapper(model.eval())

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    onnx_path = out / "minipoke.onnx"
    torch.onnx.export(
        wrapper, dummy_inputs(), str(onnx_path),
        input_names=INPUTS, output_names=["logits", "value"],
        dynamic_axes={**{n: {0: "batch"} for n in INPUTS}, "logits": {0: "batch"}, "value": {0: "batch"}},
        opset_version=17, dynamo=False,
    )
    write_spec(out / "encoder_spec.json")
    print(f"wrote {onnx_path} and {out / 'encoder_spec.json'}")

    # Parity check against PyTorch, if onnxruntime is installed
    try:
        import onnxruntime as ort
    except ImportError:
        print("(pip install onnxruntime to run the parity check)")
        return
    x = dummy_inputs(3)
    with torch.no_grad():
        ref_logits, ref_v = wrapper(*x)
    sess = ort.InferenceSession(str(onnx_path))
    got_logits, got_v = sess.run(None, {n: t.numpy() for n, t in zip(INPUTS, x)})
    print("max |logits diff|:", float(np.abs(got_logits - ref_logits.numpy()).max()),
          " max |value diff|:", float(np.abs(got_v - ref_v.numpy()).max()))


if __name__ == "__main__":
    main()
