"""
Labels for the auxiliary heads (training only), from the other seat of the same
battle. In mirror and pool games both seats are RLPlayers of this process: each
one reports its decisions and its own team (true sets) to an AuxLabeler, and after
the iteration the learner's steps get:

  aux_action        the opponent's action in the same turn, in OUR view's 14-way
                    space: switch to the opponent slot (V1 order of their revealed
                    Pokémon) of the target, or the next unrevealed slot if it was not
                    revealed yet; move j = its index among the opponent active's
                    revealed moves, or the next index if not revealed yet (+4 with
                    tera). -1 when unknown (forced switches, heuristic games...).
  aux_action_mask   the opponent's legal actions mapped the same way (all True when
                    unknown)
  aux_item, aux_ability, aux_tera, aux_moves
                    per opponent slot (revealed ones): its true item (V1 hashed id,
                    PAD for none), base ability (vocab id), tera type (V1 type index)
                    and moves (vocab ids, PAD padded), -1 / PAD when unknown; read from
                    the other seat's first request

Seats are matched by battle tag and role, decisions by turn (regular decisions only),
Pokémon by base species (one per team in random battles).
"""

from __future__ import annotations

import threading
from typing import Dict, List, Optional, Tuple

import numpy as np
from poke_env.data import to_id_str

import encoder as E
from encoder_v2 import TYPE_INDEX, item_hash

NO_LABEL = -1


def base_species(species: str) -> str:
    entry = E._GD.pokedex.get(to_id_str(species), {})
    return to_id_str(entry.get("baseSpecies") or species)


def empty_labels() -> Dict[str, np.ndarray]:
    return {
        "aux_action": np.array(NO_LABEL, dtype=np.int64),
        "aux_action_mask": np.ones(E.ACT_DIM, dtype=bool),
        "aux_item": np.full(E.N_SLOTS, NO_LABEL, dtype=np.int64),
        "aux_ability": np.full(E.N_SLOTS, NO_LABEL, dtype=np.int64),
        "aux_tera": np.full(E.N_SLOTS, NO_LABEL, dtype=np.int64),
        "aux_moves": np.zeros((E.N_SLOTS, E.N_MOVES), dtype=np.int64),
    }


def true_sets(request: Optional[dict]) -> Dict[str, Tuple[int, int, int, List[int]]]:
    """Base species -> (item, base ability, tera type, moves) labels, from a seat's own
    request (poke-env 0.16.1 does not keep the request's teraType on its Pokemon)."""
    out = {}
    for p in ((request or {}).get("side") or {}).get("pokemon", []):
        item = to_id_str(p.get("item") or "")
        out[base_species(p["details"].split(",")[0])] = (
            item_hash(item) if item else E.PAD_ID,
            E.ABILITY_VOCAB.get(to_id_str(p.get("baseAbility") or ""), E.UNK_ID),
            TYPE_INDEX.get(to_id_str(p.get("teraType") or ""), NO_LABEL),
            [E.MOVE_VOCAB.get(to_id_str(m), E.UNK_ID) for m in p.get("moves", [])[:E.N_MOVES]])
    return out


class AuxLabeler:
    def __init__(self):
        self._lock = threading.Lock()
        # (tag, role, turn) -> (action, legal mask, own team base species, active's move ids)
        self.decisions: Dict[Tuple[str, str, int], tuple] = {}
        # (tag, role) -> base species -> true set
        self.teams: Dict[Tuple[str, str], Dict[str, tuple]] = {}

    def clear(self) -> None:
        with self._lock:
            self.decisions.clear()
            self.teams.clear()

    def decision(self, battle, action: int, mask: np.ndarray) -> dict:
        """Called by every reporting seat at every decision. Returns the context the
        learner's step keeps until labelling (what the battle looked like from it)."""
        tag, role, turn = battle.battle_tag, battle.player_role, battle.turn
        team = list(battle.team.values())[:E.N_SLOTS]
        active = battle.active_pokemon
        with self._lock:
            if (tag, role) not in self.teams:  # first request: every item still held
                self.teams[(tag, role)] = true_sets(battle.last_request)
            if not battle.force_switch:
                self.decisions[(tag, role, turn)] = (
                    int(action), np.asarray(mask, dtype=bool).copy(),
                    [base_species(m.species) for m in team],
                    list(active.moves)[:E.N_MOVES] if active is not None else [])
        opp = battle.opponent_active_pokemon
        return {"tag": tag, "role": role, "turn": turn, "forced": bool(battle.force_switch),
                "species": [base_species(m.species)
                            for m in list(battle.opponent_team.values())[:E.N_SLOTS]],
                "opp_moves": list(opp.moves)[:E.N_MOVES] if opp is not None else []}

    @staticmethod
    def _map(action: int, their_team: List[str], their_moves: List[str],
             ctx: dict) -> Optional[int]:
        """Their action -> our view's index (None when it cannot be told apart)."""
        if action < E.N_SLOTS:
            if action >= len(their_team):
                return None
            target = their_team[action]
            seen = ctx["species"]
            idx = seen.index(target) if target in seen else len(seen)
            return idx if idx < E.N_SLOTS else None
        j = (action - E.N_SLOTS) % E.N_MOVES
        if j >= len(their_moves):
            return None
        seen = ctx["opp_moves"]
        idx = seen.index(their_moves[j]) if their_moves[j] in seen else len(seen)
        if idx >= E.N_MOVES:
            return None
        return E.N_SLOTS + idx + (E.N_MOVES if action >= E.N_SLOTS + E.N_MOVES else 0)

    def labels(self, ctx: Optional[dict]) -> Dict[str, np.ndarray]:
        out = empty_labels()
        if ctx is None:
            return out
        opp_role = "p2" if ctx["role"] == "p1" else "p1"
        with self._lock:
            theirs = self.decisions.get((ctx["tag"], opp_role, ctx["turn"]))
            sets = self.teams.get((ctx["tag"], opp_role))
        if theirs is not None and not ctx["forced"]:
            action, mask, their_team, their_moves = theirs
            label = self._map(action, their_team, their_moves, ctx)
            if label is not None:
                out["aux_action"] = np.array(label, dtype=np.int64)
                legal = [self._map(int(a), their_team, their_moves, ctx) for a in np.flatnonzero(mask)]
                out["aux_action_mask"] = np.zeros(E.ACT_DIM, dtype=bool)
                out["aux_action_mask"][[a for a in legal if a is not None] + [label]] = True
        if sets is not None:
            for j, species in enumerate(ctx["species"]):
                if species in sets:
                    item, ability, tera, moves = sets[species]
                    out["aux_item"][j], out["aux_ability"][j], out["aux_tera"][j] = item, ability, tera
                    out["aux_moves"][j, :len(moves)] = moves
        return out

    def label_episodes(self, episodes: List[List[dict]]) -> float:
        """Replaces each step's "aux_ctx" by its labels. -> share of steps with an
        opponent action label."""
        n = labelled = 0
        for ep in episodes:
            for step in ep:
                step["aux"] = self.labels(step.pop("aux_ctx", None))
                n += 1
                labelled += int(step["aux"]["aux_action"] >= 0)
        return labelled / max(n, 1)
