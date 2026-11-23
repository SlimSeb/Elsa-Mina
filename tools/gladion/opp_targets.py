"""
Training targets of the opponent heads (model.py `opp_heads`, offline.py `opp_coef`): for each
decision, what the player could not see. A re-simulation (resim.py) holds both sides' requests
and choices, so human_data.py stores them next to each decision.

Pairing. Showdown sends every side a request in each batch (a {"wait": true} one to a side with
nothing to choose), so the k-th record of res.requests["p1"] pairs with the k-th of
res.requests["p2"]: the opponent's record at our decision's index says what it chose from the
same state. A battle whose two sides have different request counts gets no choice targets.

The opponent's choice is one class in our view's terms (K = the encoder's candidate moves,
obs["cand_ids"].shape[1], 0 for V1; their active a = argmax of mon_feats[6:12, 3]):
    0-5        switch to our view's opposing slot j (battle.opponent_team key order)
    6          switch to a Pokémon not revealed yet
    7-10       their active's known move slot j (obs move_ids[6 + a])
    11..10+K   candidate move k (obs cand_ids[a])
    11+K       any other move
and opp_tera is 1 / 0 for a move choice (-1 when the request offers no terastallization). Both
are -1 (no target) for a wait record, a choice that is neither a switch nor a move, a move
outside MOVE_VOCAB (recharge), and a move while our view has no opposing active.

Real sets come from the opponent's first non-wait request (items not consumed yet), for each of
our view's opposing slots (by ident, "p2: Garchomp"): the item as encoder.item_id hashes it
(PAD_ID for none), the base ability (ABILITY_VOCAB), the tera type (an index into
encoder.TYPES) and the moves (MOVE_VOCAB ids, 0 = none). Slots not revealed yet: -1.
"""

from __future__ import annotations

import json
from types import SimpleNamespace
from typing import Dict, List, Optional, Sequence, Tuple

import numpy as np

import encoder as E

OPP_SWITCH0 = 0                        # 0-5: switch to our view's opposing slot j
OPP_UNREVEALED = E.N_SLOTS             # 6: switch to a Pokémon not revealed yet
OPP_MOVE0 = OPP_UNREVEALED + 1         # 7-10: their active's known move slot j
OPP_CAND0 = OPP_MOVE0 + E.N_MOVES      # 11..10+K: candidate move k
NO_TARGET = -1

# human_data.py's extra dataset fields (dataset.py): name -> (row shape, dtype)
OPP_EXTRA = {"opp_act": ((), "int16"), "opp_tera": ((), "int8"),
             "opp_item": ((E.N_SLOTS,), "int16"), "opp_ability": ((E.N_SLOTS,), "int16"),
             "opp_tera_type": ((E.N_SLOTS,), "int8"), "opp_moves": ((E.N_SLOTS, E.N_MOVES), "int16")}
TERA_TYPES = [t.name.lower() for t in E.TYPES]


def opp_other(k: int) -> int:
    """The "any other move" class with K candidate moves."""
    return OPP_CAND0 + k


def n_opp_classes(k: int) -> int:
    return opp_other(k) + 1


def parse_request(line: str) -> dict:
    """A side's "|request|{...}" line as JSON ({} for any other line)."""
    return json.loads(line[len("|request|"):]) if line.startswith("|request|") else {}


def item_index(item: Optional[str]) -> int:
    """A request's item id as encoder.item_id hashes a Pokémon's (PAD_ID for none)."""
    return E.item_id(SimpleNamespace(item=item or ""))


def set_targets(request: Optional[dict], opponent_team: Sequence[str]) -> Dict[str, np.ndarray]:
    """The real item, ability, tera type and moves of each of our view's opposing slots, from
    the opponent's `request` (its first non-wait one); -1 for slots not revealed yet."""
    item = np.full(E.N_SLOTS, NO_TARGET, dtype=np.int16)
    ability = np.full(E.N_SLOTS, NO_TARGET, dtype=np.int16)
    tera = np.full(E.N_SLOTS, NO_TARGET, dtype=np.int8)
    moves = np.full((E.N_SLOTS, E.N_MOVES), NO_TARGET, dtype=np.int16)
    by_ident = {p.get("ident"): p for p in ((request or {}).get("side") or {}).get("pokemon", [])}
    for j, ident in enumerate(list(opponent_team)[:E.N_SLOTS]):
        mon = by_ident.get(ident)
        if mon is None:
            continue
        item[j] = item_index(mon.get("item"))
        ability[j] = E.ABILITY_VOCAB.get(E.to_id_str(mon.get("baseAbility") or ""), NO_TARGET)
        tera_type = (mon.get("teraType") or "").lower()
        tera[j] = TERA_TYPES.index(tera_type) if tera_type in TERA_TYPES else NO_TARGET
        ids = [E.MOVE_VOCAB.get(E.to_id_str(m), E.UNK_ID) for m in (mon.get("moves") or [])][:E.N_MOVES]
        moves[j] = ids + [E.PAD_ID] * (E.N_MOVES - len(ids))
    return {"opp_item": item, "opp_ability": ability, "opp_tera_type": tera, "opp_moves": moves}


def choice_target(obs: Dict[str, np.ndarray], opponent_team: Sequence[str], choice,
                  request: dict) -> Tuple[int, int]:
    """(opp_act, opp_tera) of the opponent's `choice` (analyze_replays.Choice, None for none),
    answering its `request`, in the terms of our view `obs` (its encoder's arrays)."""
    if choice is None:
        return NO_TARGET, NO_TARGET
    if choice.kind == "switch":
        team = (request.get("side") or {}).get("pokemon", [])
        if choice.position is None or not 1 <= choice.position <= len(team):
            return NO_TARGET, NO_TARGET
        ident = team[choice.position - 1].get("ident")
        if ident not in opponent_team:
            return OPP_UNREVEALED, NO_TARGET
        slot = list(opponent_team).index(ident)
        return (OPP_SWITCH0 + slot if slot < E.N_SLOTS else NO_TARGET), NO_TARGET
    if choice.kind != "move" or not choice.move:
        return NO_TARGET, NO_TARGET
    active = (request.get("active") or [{}])[0]
    move = choice.move
    if move.isdigit():                       # "move 2": the request's second move
        options = active.get("moves") or []
        if not 1 <= int(move) <= len(options):
            return NO_TARGET, NO_TARGET
        option = options[int(move) - 1]
        move = option.get("id") or E.to_id_str(option.get("move") or "")
    move_id = E.MOVE_VOCAB.get(move)
    active_flags = np.asarray(obs["mon_feats"])[E.N_SLOTS:, 3]
    if move_id is None or active_flags.max() <= 0:
        return NO_TARGET, NO_TARGET
    a = int(active_flags.argmax())
    known = np.flatnonzero(np.asarray(obs["move_ids"])[E.N_SLOTS + a] == move_id)
    cands = np.asarray(obs["cand_ids"])[a] if "cand_ids" in obs else np.zeros(0, dtype=np.int64)
    candidate = np.flatnonzero(cands == move_id)
    if len(known):
        act = OPP_MOVE0 + int(known[0])
    elif len(candidate):
        act = OPP_CAND0 + int(candidate[0])
    else:
        act = opp_other(len(cands))
    return act, (int(choice.tera) if active.get("canTerastallize") else NO_TARGET)


def side_targets(res, side: str, decisions: Sequence[dict],
                 key: str = "s") -> Tuple[List[Dict[str, object]], bool]:
    """The opponent targets of each of `side`'s decisions (resim.side_decisions' decision
    points, with their `opponent_team` and their observation under obs[key]) in the
    re-simulation `res`, and whether the two sides' requests pair (equal counts: if not, every
    choice target is -1)."""
    from resim import SIDES, as_choice
    opp = SIDES[1 - SIDES.index(side)]
    ours, theirs = res.requests.get(side) or [], res.requests.get(opp) or []
    opp_lines = res.sides.get(opp) or []
    paired = len(ours) == len(theirs)
    index = {record["line"]: k for k, record in enumerate(ours)}
    first = next((parse_request(opp_lines[r["line"]]) for r in theirs if not r["wait"]), None)
    out = []
    for d in decisions:
        obs, team = d["obs"][key], d.get("opponent_team") or []
        act, tera = NO_TARGET, NO_TARGET
        k = index.get(d["line"]) if paired else None
        if k is not None and not theirs[k]["wait"]:
            record = theirs[k]
            act, tera = choice_target(obs, team, as_choice(opp, record["choice"]),
                                      parse_request(opp_lines[record["line"]]))
        out.append({"opp_act": act, "opp_tera": tera, **set_targets(first, team)})
    return out, paired
