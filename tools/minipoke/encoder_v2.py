"""
Encoder v2 (minipoke-v2): V1's observation, untouched (encoder.py), plus what
Showdown's random battle sets say about each revealed opponent Pokémon
(setpriors.py).

Arrays added after V1's six (opponent slots 6-11 are rows 0-5, the same order as
V1's slots; unrevealed slots and species without sets stay all zeros):

    cand_ids     int64 [6, K]               candidate moves (setpriors ranking), PAD = none
    cand_feats   f32   [6, K, F_MOVE + 1]   V1 move features of each candidate (owner =
                                             that Pokémon, target = our active, pp 1), then
                                             the fraction of consistent roles with it
    prior_ids    int64 [6, 2, A]            row 0: ability ids (V1 vocabulary), row 1: item
                                             ids (V1 hashing), highest weight first, ties by id
    prior_w      f32   [6, 2, A]            their weights, 0 for padding
    prior_feats  f32   [6, F_PRIOR]         tera type weights (V1 type order), species has
                                             sets, n consistent / n roles, fallback, Choice
                                             item possible

The action space and its mapping onto poke-env orders are V1's (encoder.py).
"""

from __future__ import annotations

import zlib
from typing import Dict, List, Optional, Tuple

import numpy as np
from poke_env.battle import Battle, Move, Pokemon

import encoder as E
from setpriors import SetPriors

A_PRIOR = 4                              # abilities / items kept per Pokémon
F_CAND = E.F_MOVE + 1
F_PRIOR = E.NT + 4
TYPE_INDEX = {t.name.lower(): i for i, t in enumerate(E.TYPES)}

Layout = Dict[str, Tuple[Tuple[int, ...], str]]

V1_LAYOUT: Layout = {
    "mon_ids": ((E.N_MONS, 3), "int64"),
    "move_ids": ((E.N_MONS, E.N_MOVES), "int64"),
    "mon_feats": ((E.N_MONS, E.F_MON), "float32"),
    "move_feats": ((E.N_MONS, E.N_MOVES, E.F_MOVE), "float32"),
    "field": ((E.F_FIELD,), "float32"),
    "mask": ((E.ACT_DIM,), "bool"),
}


def item_hash(item: str) -> int:
    """V1's item id of a known item (encoder.item_id)."""
    return 2 + zlib.crc32(item.encode()) % E.N_ITEM_BUCKETS


class EncoderV1:
    """encoder.py as is."""
    version = 1

    def __init__(self):
        self.layout: Layout = dict(V1_LAYOUT)

    @property
    def input_names(self) -> List[str]:
        return list(self.layout)

    def encode(self, battle: Battle) -> Dict[str, np.ndarray]:
        return E.encode(battle)


class EncoderV2(EncoderV1):
    version = 2

    def __init__(self, priors: SetPriors, cand_moves: int = 8):
        super().__init__()
        self.priors = priors
        self.k = cand_moves
        self.layout.update({
            "cand_ids": ((E.N_SLOTS, cand_moves), "int64"),
            "cand_feats": ((E.N_SLOTS, cand_moves, F_CAND), "float32"),
            "prior_ids": ((E.N_SLOTS, 2, A_PRIOR), "int64"),
            "prior_w": ((E.N_SLOTS, 2, A_PRIOR), "float32"),
            "prior_feats": ((E.N_SLOTS, F_PRIOR), "float32"),
        })
        self._moves: Dict[str, Optional[Move]] = {}

    def _move(self, move_id: str) -> Optional[Move]:
        """A fresh (full pp) poke-env Move, cached: its features never change."""
        if move_id not in self._moves:
            self._moves[move_id] = Move(move_id, gen=9) if move_id in E._GD.moves else None
        return self._moves[move_id]

    def encode(self, battle: Battle) -> Dict[str, np.ndarray]:
        obs = E.encode(battle)
        obs.update(self.encode_priors(battle))
        return obs

    def encode_priors(self, battle: Battle) -> Dict[str, np.ndarray]:
        k = self.k
        cand_ids = np.zeros((E.N_SLOTS, k), dtype=np.int64)
        cand_feats = np.zeros((E.N_SLOTS, k, F_CAND), dtype=np.float32)
        prior_ids = np.zeros((E.N_SLOTS, 2, A_PRIOR), dtype=np.int64)
        prior_w = np.zeros((E.N_SLOTS, 2, A_PRIOR), dtype=np.float32)
        prior_feats = np.zeros((E.N_SLOTS, F_PRIOR), dtype=np.float32)
        my_active = battle.active_pokemon
        for row, mon in enumerate(list(battle.opponent_team.values())[:E.N_SLOTS]):
            p = self.priors.infer_mon(mon, k)
            if not p.found:
                continue
            for j, (move_id, frac) in enumerate(p.candidates):
                cand_ids[row, j] = E.MOVE_VOCAB.get(move_id, E.UNK_ID)
                move = self._move(move_id)
                if move is not None:
                    cand_feats[row, j, :E.F_MOVE] = E._move_features(move, mon, my_active)
                else:
                    cand_feats[row, j, 0] = 1.0
                cand_feats[row, j, E.F_MOVE] = frac
            for j, (ability, w) in enumerate(p.abilities[:A_PRIOR]):
                prior_ids[row, 0, j] = E.ABILITY_VOCAB.get(ability, E.UNK_ID)
                prior_w[row, 0, j] = w
            for j, (item, w) in enumerate(p.items[:A_PRIOR]):
                prior_ids[row, 1, j] = item_hash(item)
                prior_w[row, 1, j] = w
            for tera, w in p.tera_types.items():
                if tera in TYPE_INDEX:
                    prior_feats[row, TYPE_INDEX[tera]] = w
            prior_feats[row, E.NT] = 1.0
            prior_feats[row, E.NT + 1] = p.n_consistent / p.n_roles
            prior_feats[row, E.NT + 2] = float(p.fallback)
            prior_feats[row, E.NT + 3] = float(p.choice_possible)
        return {"cand_ids": cand_ids, "cand_feats": cand_feats, "prior_ids": prior_ids,
                "prior_w": prior_w, "prior_feats": prior_feats}

    def spec(self) -> dict:
        """encoder_spec.json's description of the v2 additions."""
        return {
            "set_priors": {
                "file": "set_priors.json",
                "sha256": self.priors.sha256,
                "source_sha256": self.priors.source_sha256,
                "source_files": "pokemon-showdown/data/random-battles/gen9/sets.json "
                                "(data/random-sets.json before May 2024)",
                "candidate_moves_k": self.k,
                "abilities_items_a": A_PRIOR,
                "species_lookup": "species id, else its dex battleOnly form(s), then baseSpecies",
                "evidence": "revealed moves, revealed base ability, revealed item, tera type once "
                            "terastallized; evidence outside the species' whole pool is ignored",
                "consistent_roles": "roles containing every piece of evidence; none -> all "
                                    "roles of the species (fallback)",
                "candidate_ranking": "moves of the consistent roles not revealed yet, by "
                                     "fraction of consistent roles containing them (descending), "
                                     "ties by move id (ascending, ordinal), first K kept",
                "weights": "abilities / items / tera types: mean over consistent roles of "
                           "[x in role] / |role's x|, summed in role order in float64; a "
                           "revealed one has weight 1; sorted by weight descending, ties by id",
                "choice_possible": "revealed Choice Band/Scarf/Specs; or item unknown, no "
                                   "revealed status move except trick/switcheroo/healingwish, "
                                   "and a consistent role with >= min(4, pool size) damaging "
                                   "moves or one of those three moves; a known empty item -> 0",
                "items": "gen 9 sets have no items: the item row is all padding",
            },
            "orderings": {
                "prior_feats": ["tera weight x " + str(E.NT) + " (types order)", "has_sets",
                                "n_consistent / n_roles", "fallback", "choice_possible"],
                "cand_feats": ["V1 move features x " + str(E.F_MOVE), "fraction"],
            },
        }


def encoder_for(cfg, priors: Optional[SetPriors] = None):
    """The observation encoder of a model with this ModelConfig."""
    if getattr(cfg, "encoder_version", 1) == 1:
        return EncoderV1()
    if priors is None:
        raise ValueError("an encoder v2 model needs its set priors (model.priors)")
    return EncoderV2(priors, cfg.cand_moves)


def model_encoder(model):
    return encoder_for(model.cfg, getattr(model, "priors", None))


def random_obs(layout: Layout, batch: int, seed: int = 0) -> Dict[str, np.ndarray]:
    """Plausible random arrays for the v2 additions (V1's are export_onnx.random_inputs)."""
    rng = np.random.default_rng(seed)
    out = {}
    for name, (shape, dtype) in layout.items():
        if name in V1_LAYOUT:
            continue
        if dtype == "int64":
            high = E.VOCAB_SIZES["move"] if name == "cand_ids" else E.VOCAB_SIZES["ability"]
            arr = rng.integers(0, high, size=(batch, *shape))
            if name == "prior_ids":
                arr[..., 1, :] = rng.integers(0, E.VOCAB_SIZES["item"], size=(batch, *shape[:-2], shape[-1]))
        else:
            arr = rng.random(size=(batch, *shape)).astype(np.float32)
            if name == "cand_feats":
                arr[..., 0] = (arr[..., 0] > 0.3).astype(np.float32)
        out[name] = arr.astype(np.int64 if dtype == "int64" else np.float32)
    return out
