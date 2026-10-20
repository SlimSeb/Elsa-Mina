"""
Turn a poke-env ``Battle`` into fixed-shape numpy arrays for the model.

Layout (one observation):
    mon_ids    int64  [12, 3]        species, item, ability ids per Pokémon
    move_ids   int64  [12, 4]        move ids per Pokémon
    mon_feats  f32    [12, F_MON]    per-Pokémon scalars / one-hots
    move_feats f32    [12, 4, F_MOVE]
    field      f32    [F_FIELD]      weather, terrain, hazards, counters
    mask       bool   [14]           legal actions

Token order: slots 0-5 are *our* team in ``battle.team`` order (so switch
action i == token i), slots 6-11 are the opponent's revealed Pokémon, padded
with "unknown" placeholders.

Compact action space (14), mapped onto poke-env's gen-9 SinglesEnv space (26):
    0-5   switch to team slot i          -> env action 0-5
    6-9   use move j                      -> env action 6-9
    10-13 terastallize + use move j       -> env action 22-25
"""

from __future__ import annotations

import math
import zlib
from typing import Dict, List, Optional

import numpy as np
from poke_env.battle import (
    Battle,
    Effect,
    Field,
    Move,
    MoveCategory,
    Pokemon,
    PokemonType,
    SideCondition,
    Status,
    Weather,
)
from poke_env.data import GenData, to_id_str
from poke_env.environment import SinglesEnv

N_SLOTS = 6
N_MONS = 2 * N_SLOTS
N_MOVES = 4
ACT_DIM = 14

# --------------------------------------------------------------------------
# Vocabularies (built once from poke-env's bundled gen-9 data)
# --------------------------------------------------------------------------
# id 0 = none / padding, id 1 = unknown, real entries start at 2.
PAD_ID, UNK_ID = 0, 1
N_ITEM_BUCKETS = 1024  # items have no bundled table -> stable hashing


def _build_vocab(names) -> Dict[str, int]:
    return {n: i + 2 for i, n in enumerate(sorted(set(names)))}


_GD = GenData.from_gen(9)
SPECIES_VOCAB = _build_vocab(to_id_str(k) for k in _GD.pokedex)
MOVE_VOCAB = _build_vocab(to_id_str(k) for k in _GD.moves)
ABILITY_VOCAB = _build_vocab(
    to_id_str(a)
    for entry in _GD.pokedex.values()
    for a in (entry.get("abilities") or {}).values()
)

VOCAB_SIZES = {
    "species": len(SPECIES_VOCAB) + 2,
    "item": N_ITEM_BUCKETS + 2,
    "ability": len(ABILITY_VOCAB) + 2,
    "move": len(MOVE_VOCAB) + 2,
}


def species_id(mon: Optional[Pokemon]) -> int:
    if mon is None:
        return UNK_ID
    return SPECIES_VOCAB.get(to_id_str(mon.species), UNK_ID)


def item_id(mon: Optional[Pokemon]) -> int:
    if mon is None or mon.item == GenData.UNKNOWN_ITEM:
        return UNK_ID
    if not mon.item:  # no item / item consumed
        return PAD_ID
    return 2 + zlib.crc32(mon.item.encode()) % N_ITEM_BUCKETS


def ability_id(mon: Optional[Pokemon]) -> int:
    if mon is None:
        return UNK_ID
    ab = mon.ability
    if ab is None and mon.possible_abilities and len(mon.possible_abilities) == 1:
        ab = mon.possible_abilities[0]
    if ab is None:
        return UNK_ID
    return ABILITY_VOCAB.get(to_id_str(ab), UNK_ID)


def move_id(move: Optional[Move]) -> int:
    if move is None:
        return PAD_ID
    return MOVE_VOCAB.get(to_id_str(move.id), UNK_ID)


# --------------------------------------------------------------------------
# Feature dimensions
# --------------------------------------------------------------------------
TYPES: List[PokemonType] = list(PokemonType)
STATUSES = [Status.BRN, Status.FNT, Status.FRZ, Status.PAR, Status.PSN, Status.SLP, Status.TOX]
BOOSTS = ["atk", "def", "spa", "spd", "spe", "accuracy", "evasion"]
STATS = ["hp", "atk", "def", "spa", "spd", "spe"]
EFFECTS = [
    Effect.CONFUSION, Effect.LEECH_SEED, Effect.SUBSTITUTE, Effect.TAUNT,
    Effect.ENCORE, Effect.YAWN, Effect.PERISH1, Effect.PARTIALLY_TRAPPED,
    Effect.SALT_CURE, Effect.FOCUS_ENERGY,
]
WEATHERS: List[Weather] = list(Weather)
FIELDS: List[Field] = list(Field)
SIDE_CONDS: List[SideCondition] = list(SideCondition)
STACKABLE = {SideCondition.SPIKES: 3, SideCondition.TOXIC_SPIKES: 2}
CATEGORIES = [MoveCategory.PHYSICAL, MoveCategory.SPECIAL, MoveCategory.STATUS]

NT = len(TYPES)
F_MON = (
    6                 # present, revealed-placeholder, is_mine, active, fainted, hp
    + len(STATUSES)
    + len(BOOSTS)
    + NT              # current types
    + NT + 1          # tera type (if known), terastallized
    + len(STATS) + 1  # base stats, level
    + 2               # item known, ability known
    + len(EFFECTS)
    + 3               # must_recharge, first_turn, preparing
    + 1               # known moves / 4
)
F_MOVE = (
    5                 # present, base power, accuracy, pp fraction, priority
    + len(CATEGORIES)
    + NT
    + 3               # effectiveness vs opposing active (log2), immune, STAB
    + 8               # heal, drain, recoil, protect, pivot, hazard/side cond, inflicts status, forces switch
)
F_FIELD = (
    len(WEATHERS)
    + len(FIELDS)
    + 2 * len(SIDE_CONDS)
    + 9  # turn, can_tera, used_tera, opp_used_tera, force_switch, trapped, alive mine/opp, opp revealed
)


def _onehot(vec: np.ndarray, offset: int, idx: Optional[int]) -> None:
    if idx is not None and idx >= 0:
        vec[offset + idx] = 1.0


# --------------------------------------------------------------------------
# Per-entity encoders
# --------------------------------------------------------------------------
def _mon_features(mon: Optional[Pokemon], is_mine: bool) -> np.ndarray:
    f = np.zeros(F_MON, dtype=np.float32)
    o = 0
    if mon is None:  # unrevealed opponent slot
        f[0], f[1], f[2] = 0.0, 1.0, float(is_mine)
        return f
    f[0] = 1.0
    f[2] = float(is_mine)
    f[3] = float(mon.active)
    f[4] = float(mon.fainted)
    f[5] = float(mon.current_hp_fraction or 0.0)
    o = 6
    if mon.status in STATUSES:
        f[o + STATUSES.index(mon.status)] = 1.0
    o += len(STATUSES)
    for i, b in enumerate(BOOSTS):
        f[o + i] = mon.boosts.get(b, 0) / 6.0
    o += len(BOOSTS)
    for t in mon.types:
        if t in TYPES:
            f[o + TYPES.index(t)] = 1.0
    o += NT
    if mon.tera_type in TYPES:
        f[o + TYPES.index(mon.tera_type)] = 1.0
    o += NT
    f[o] = float(mon.is_terastallized)
    o += 1
    for i, s in enumerate(STATS):
        f[o + i] = (mon.base_stats or {}).get(s, 0) / 200.0
    o += len(STATS)
    f[o] = (mon.level or 0) / 100.0
    o += 1
    f[o] = float(mon.item != GenData.UNKNOWN_ITEM)
    f[o + 1] = float(mon.ability is not None)
    o += 2
    for i, e in enumerate(EFFECTS):
        f[o + i] = float(e in mon.effects)
    o += len(EFFECTS)
    f[o] = float(bool(mon.must_recharge))
    f[o + 1] = float(bool(mon.first_turn))
    f[o + 2] = float(bool(mon.preparing))
    o += 3
    f[o] = min(len(mon.moves), 4) / 4.0
    return f


def _move_features(
    move: Move, owner: Pokemon, target: Optional[Pokemon]
) -> np.ndarray:
    f = np.zeros(F_MOVE, dtype=np.float32)
    f[0] = 1.0
    f[1] = min(move.base_power, 250) / 150.0
    f[2] = float(move.accuracy)
    f[3] = (move.current_pp / move.max_pp) if move.max_pp else 1.0
    f[4] = max(-7, min(5, move.priority)) / 5.0
    o = 5
    if move.category in CATEGORIES:
        f[o + CATEGORIES.index(move.category)] = 1.0
    o += len(CATEGORIES)
    if move.type in TYPES:
        f[o + TYPES.index(move.type)] = 1.0
    o += NT
    if target is not None and move.category != MoveCategory.STATUS:
        try:
            mult = target.damage_multiplier(move)
        except Exception:
            mult = 1.0
        f[o] = 0.0 if mult == 0 else max(-2.0, min(2.0, math.log2(mult))) / 2.0
        f[o + 1] = float(mult == 0)
    f[o + 2] = float(move.type in owner.types)
    o += 3
    f[o] = float(move.heal or 0.0)
    f[o + 1] = float(move.drain or 0.0)
    f[o + 2] = float(move.recoil or 0.0)
    f[o + 3] = float(bool(move.is_protect_move))
    f[o + 4] = float(bool(move.self_switch))
    f[o + 5] = float(move.side_condition is not None)
    f[o + 6] = float(move.status is not None)
    f[o + 7] = float(bool(move.force_switch))
    return f


def _side_features(conds: Dict[SideCondition, int]) -> np.ndarray:
    f = np.zeros(len(SIDE_CONDS), dtype=np.float32)
    for c, v in conds.items():
        if c in SIDE_CONDS:
            cap = STACKABLE.get(c)
            f[SIDE_CONDS.index(c)] = min(v, cap) / cap if cap else 1.0
    return f


# --------------------------------------------------------------------------
# Actions
# --------------------------------------------------------------------------
def action_mask(battle: Battle) -> np.ndarray:
    full = SinglesEnv.get_action_mask(battle)  # length 26 in gen 9
    compact = list(full[0:10]) + list(full[22:26])
    return np.asarray(compact, dtype=bool)


def compact_to_env_action(a: int) -> int:
    return a if a < 10 else a + 12


def env_to_compact_action(a: int) -> Optional[int]:
    if 0 <= a < 10:
        return a
    if 22 <= a < 26:
        return a - 12
    return None  # mega / z / dynamax: not used in gen 9


def action_to_order(a: int, battle: Battle):
    return SinglesEnv.action_to_order(
        np.int64(compact_to_env_action(int(a))), battle, strict=False
    )


def order_to_compact_action(order, battle: Battle) -> Optional[int]:
    try:
        a = int(SinglesEnv.order_to_action(order, battle, strict=True))
    except Exception:
        return None
    return env_to_compact_action(a)


# --------------------------------------------------------------------------
# Full observation
# --------------------------------------------------------------------------
def encode(battle: Battle) -> Dict[str, np.ndarray]:
    mine: List[Optional[Pokemon]] = list(battle.team.values())[:N_SLOTS]
    theirs: List[Optional[Pokemon]] = list(battle.opponent_team.values())[:N_SLOTS]
    mine += [None] * (N_SLOTS - len(mine))
    theirs += [None] * (N_SLOTS - len(theirs))

    my_active = battle.active_pokemon
    opp_active = battle.opponent_active_pokemon

    mon_ids = np.zeros((N_MONS, 3), dtype=np.int64)
    move_ids = np.zeros((N_MONS, N_MOVES), dtype=np.int64)
    mon_feats = np.zeros((N_MONS, F_MON), dtype=np.float32)
    move_feats = np.zeros((N_MONS, N_MOVES, F_MOVE), dtype=np.float32)

    for slot, (mon, is_mine) in enumerate(
        [(m, True) for m in mine] + [(m, False) for m in theirs]
    ):
        mon_ids[slot] = (species_id(mon), item_id(mon), ability_id(mon))
        mon_feats[slot] = _mon_features(mon, is_mine)
        if mon is None:
            continue
        target = opp_active if is_mine else my_active
        # Same ordering poke-env uses for move actions 6-9.
        for j, mv in enumerate(list(mon.moves.values())[:N_MOVES]):
            move_ids[slot, j] = move_id(mv)
            move_feats[slot, j] = _move_features(mv, mon, target)

    field = np.zeros(F_FIELD, dtype=np.float32)
    o = 0
    for w in battle.weather:
        if w in WEATHERS:
            field[o + WEATHERS.index(w)] = 1.0
    o += len(WEATHERS)
    for fl in battle.fields:
        if fl in FIELDS:
            field[o + FIELDS.index(fl)] = 1.0
    o += len(FIELDS)
    field[o : o + len(SIDE_CONDS)] = _side_features(battle.side_conditions)
    o += len(SIDE_CONDS)
    field[o : o + len(SIDE_CONDS)] = _side_features(battle.opponent_side_conditions)
    o += len(SIDE_CONDS)
    my_alive = sum(1 for m in battle.team.values() if not m.fainted)
    opp_fainted = sum(1 for m in battle.opponent_team.values() if m.fainted)
    field[o + 0] = min(battle.turn, 50) / 50.0
    field[o + 1] = float(battle.can_tera)
    field[o + 2] = float(battle.used_tera)
    field[o + 3] = float(battle.opponent_used_tera)
    field[o + 4] = float(battle.force_switch)
    field[o + 5] = float(battle.trapped)
    field[o + 6] = my_alive / 6.0
    field[o + 7] = (6 - opp_fainted) / 6.0
    field[o + 8] = len(battle.opponent_team) / 6.0

    return {
        "mon_ids": mon_ids,
        "move_ids": move_ids,
        "mon_feats": mon_feats,
        "move_feats": move_feats,
        "field": field,
        "mask": action_mask(battle),
    }


def collate(obs_list: List[Dict[str, np.ndarray]]) -> Dict[str, np.ndarray]:
    return {k: np.stack([o[k] for o in obs_list]) for k in obs_list[0]}


def fainted_counts(battle: Battle):
    mine = sum(1 for m in battle.team.values() if m.fainted)
    theirs = sum(1 for m in battle.opponent_team.values() if m.fainted)
    return mine, theirs
