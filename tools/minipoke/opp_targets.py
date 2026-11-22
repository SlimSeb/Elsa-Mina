"""
Auxiliary targets: what a re-simulated human replay reveals beyond the player's own view.

A re-simulation (resim.py) holds both players' requests, so for each decision of one side we
know what that side could not see:

 - the opponent's choice made at the same time (Showdown sends every side a request at once,
   a wait request to a side with nothing to choose, so the k-th request of p1 and the k-th
   request of p2 were sent together), in our view's terms (`OPP_*` classes below);
 - the opponent's real sets (their first request's side.pokemon): item, base ability, tera
   type and moves, for each opposing Pokémon our view has revealed.

model.py's opponent heads (ModelConfig.opp_heads) learn them; offline.py trains them with
`opp_coef`. They are training-only: nothing here reaches an export or ElsaMina.

The opponent's choice, as one class (`n_opp_actions(K)` of them, K = the encoder's candidate
moves per opposing slot, 0 for V1):

    0-5         a switch to their Pokémon in our view's opposing slot j (battle.opponent_team order)
    6           a switch to a Pokémon we have not seen yet
    7-10        their active's known move in slot j (our view's order of its revealed moves)
    11-(10+K)   their active's candidate move k (encoder_v2's set priors candidates)
    11+K        another move
plus `opp_tera`: whether a move choice terastallized (-1: no move choice, or tera unavailable).

Classes that cannot be told from our view are -1 (no target): a wait request, a choice that is
neither a switch nor a move, a move outside the vocabulary (recharge), no opposing active.
"""

from __future__ import annotations

import json
import zlib
from typing import Dict, List, Optional, Sequence

import numpy as np

import encoder as E

OPP_SWITCH0 = 0
OPP_UNREVEALED = E.N_SLOTS                 # 6
OPP_KNOWN0 = OPP_UNREVEALED + 1            # 7
OPP_CAND0 = OPP_KNOWN0 + E.N_MOVES         # 11
N_TERA_TYPES = E.NT
TERA_INDEX = {t.name.lower(): i for i, t in enumerate(E.TYPES)}
NO_TARGET = -1

# Dataset extra fields (dataset.py), written by human_data.py
OPP_EXTRA = {
    "opp_act": ((), "int16"),
    "opp_tera": ((), "int8"),
    "opp_item": ((E.N_SLOTS,), "int16"),
    "opp_ability": ((E.N_SLOTS,), "int16"),
    "opp_tera_type": ((E.N_SLOTS,), "int8"),
    "opp_moves": ((E.N_SLOTS, E.N_MOVES), "int16"),
}


def n_opp_actions(n_cand: int) -> int:
    return OPP_CAND0 + n_cand + 1


def opp_other(n_cand: int) -> int:
    return OPP_CAND0 + n_cand


def to_id(text: Optional[str]) -> str:
    return "".join(c for c in (text or "").lower() if c.isalnum())


# --------------------------------------------------------------------------
# Vocabulary ids, as the encoder's
# --------------------------------------------------------------------------
def item_target(item: Optional[str]) -> int:
    """A request's item ("" = none) as encoder.item_id would encode it once revealed."""
    item = to_id(item)
    return E.PAD_ID if not item else 2 + zlib.crc32(item.encode()) % E.N_ITEM_BUCKETS


def ability_target(ability: Optional[str]) -> int:
    return E.ABILITY_VOCAB.get(to_id(ability), E.UNK_ID) if ability else NO_TARGET


def move_target(move: Optional[str]) -> int:
    return E.MOVE_VOCAB.get(to_id(move), E.UNK_ID) if move else E.PAD_ID


def tera_type_target(tera: Optional[str]) -> int:
    return TERA_INDEX.get(to_id(tera), NO_TARGET)


# --------------------------------------------------------------------------
# Requests
# --------------------------------------------------------------------------
def request_at(lines: Sequence[str], record: dict) -> dict:
    """The JSON of a resim.js request record's |request| line."""
    line = lines[record["line"]]
    return json.loads(line[len("|request|"):]) if line.startswith("|request|") else {}


def real_sets(lines: Sequence[str], requests: Sequence[dict]) -> Dict[str, dict]:
    """ident ("p2: Garchomp") -> its set, from the side's first non-wait request: the team as
    generated (items not yet consumed or knocked off)."""
    for record in requests:
        if record["wait"]:
            continue
        request = request_at(lines, record)
        pokemon = (request.get("side") or {}).get("pokemon") or []
        if pokemon:
            return {p["ident"]: p for p in pokemon}
    return {}


def set_targets(opponent_team: Sequence[str], sets: Dict[str, dict]) -> Dict[str, np.ndarray]:
    """Per opposing slot of our view (battle.opponent_team's first 6 keys): the real item,
    ability, tera type and moves. Slots not revealed, or whose key matches no real Pokémon
    (Illusion), are -1 (moves: 0 = none)."""
    out = {"opp_item": np.full(E.N_SLOTS, NO_TARGET, np.int16),
           "opp_ability": np.full(E.N_SLOTS, NO_TARGET, np.int16),
           "opp_tera_type": np.full(E.N_SLOTS, NO_TARGET, np.int8),
           "opp_moves": np.zeros((E.N_SLOTS, E.N_MOVES), np.int16)}
    for j, key in enumerate(list(opponent_team)[:E.N_SLOTS]):
        s = sets.get(key)
        if s is None:
            continue
        out["opp_item"][j] = item_target(s.get("item"))
        out["opp_ability"][j] = ability_target(s.get("baseAbility") or s.get("ability"))
        out["opp_tera_type"][j] = tera_type_target(s.get("teraType"))
        for k, m in enumerate((s.get("moves") or [])[:E.N_MOVES]):
            out["opp_moves"][j, k] = move_target(m)
    return out


def opp_action(choice, request: dict, opponent_team: Sequence[str], obs: Dict[str, np.ndarray]):
    """(class, tera) of the opponent's choice (analyze_replays.Choice, answering `request`) in
    the view whose observation is `obs` and opposing team keys `opponent_team`."""
    n_cand = obs["cand_ids"].shape[1] if "cand_ids" in obs else 0
    if choice is None:
        return NO_TARGET, NO_TARGET
    if choice.kind == "switch" and choice.position is not None:
        pokemon = (request.get("side") or {}).get("pokemon") or []
        if not 1 <= choice.position <= len(pokemon):
            return NO_TARGET, NO_TARGET
        ident = pokemon[choice.position - 1]["ident"]
        keys = list(opponent_team)[:E.N_SLOTS]
        return (OPP_SWITCH0 + keys.index(ident) if ident in keys else OPP_UNREVEALED), NO_TARGET
    if choice.kind != "move" or not choice.move:
        return NO_TARGET, NO_TARGET
    move = choice.move
    active = (request.get("active") or [{}])[0]
    if move.isdigit():   # "move 2": the request's second move
        listed = active.get("moves") or []
        idx = int(move) - 1
        move = to_id(listed[idx].get("id") or listed[idx].get("move")) if 0 <= idx < len(listed) else ""
    mid = E.MOVE_VOCAB.get(to_id(move))
    flags = obs["mon_feats"][E.N_SLOTS:, 3]
    if mid is None or not (flags > 0).any():
        return NO_TARGET, NO_TARGET
    a = int(np.argmax(flags))
    tera = int(bool(choice.tera)) if active.get("canTerastallize") else NO_TARGET
    known = [int(i) for i in obs["move_ids"][E.N_SLOTS + a]]
    if mid in known:
        return OPP_KNOWN0 + known.index(mid), tera
    if n_cand:
        cands = [int(i) for i in obs["cand_ids"][a]]
        if mid in cands:
            return OPP_CAND0 + cands.index(mid), tera
    return opp_other(n_cand), tera


def side_targets(res, side: str, decisions: Sequence[dict]) -> List[Dict[str, np.ndarray]]:
    """The opponent targets of each of `side`'s decision points (resim.side_decisions, encoded with
    one encoder: its "obs" holds a single observation) in re-simulation `res`."""
    from resim import SIDES, as_choice
    opp = SIDES[1 - SIDES.index(side)]
    ours, theirs = res.requests[side], res.requests[opp]
    paired = len(ours) == len(theirs)          # every batch sends both sides a request
    index_of = {r["line"]: i for i, r in enumerate(ours)}
    sets = real_sets(res.sides[opp], theirs)
    out = []
    for d in decisions:
        obs = next(iter(d["obs"].values()))
        team = d.get("opponent_team") or []
        t = {k: np.asarray(v) for k, v in set_targets(team, sets).items()}
        act, tera = NO_TARGET, NO_TARGET
        i = index_of.get(d["line"])
        if paired and i is not None and not theirs[i]["wait"] and theirs[i]["choice"] is not None:
            request = request_at(res.sides[opp], theirs[i])
            act, tera = opp_action(as_choice(opp, theirs[i]["choice"]), request, team, obs)
        t["opp_act"] = np.int16(act)
        t["opp_tera"] = np.int8(tera)
        out.append(t)
    return out
