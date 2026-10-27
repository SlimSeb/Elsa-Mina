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

With `history_tokens` K > 0 (ModelConfig.history_tokens), the last K events of the
protocol log (history.recent_events), oldest first, padded at the front:

    event_ids    int64 [K, 2]               move id (PAD for an entrance), species id
    event_feats  f32   [K, F_EVENT]         present, ours, move, entrance, dragged in,
                                             called / locked, crit, miss, super effective,
                                             resisted, immune, failed, KO,
                                             turns ago (min 10) / 10

They replace the `history` arrays below (a model with both only gets the events).

With `matchup_tokens` (ModelConfig.matchup_tokens), damage.py's estimates:

    matchup_feats f32  [12, F_MATCH]        rows 0-5: our mon i vs their active; rows 6-11:
                                             our active vs their mon j (V1 slot orders).
                                             present, expected damage fraction of each of
                                             our known moves (V1 order) on them, of each of
                                             their revealed moves on us, the max each way,
                                             we can KO, they can KO, we move first, speed
                                             tie, they move first. Rows with a missing or
                                             fainted Pokémon stay 0.

With `history` (ModelConfig.history), two more (history.py reads the protocol log):

    hist_ids     int64 [2]                  last move used by our active, by theirs (PAD)
    hist_feats   f32   [F_HIST]             turns since their / our active came in (/10,
                                             max 1), they / we switched by choice last turn,
                                             their / our consecutive protects (/3, max 1),
                                             they chose the same move twice in a row,
                                             that and a Choice item is possible (lock)

With `move_effects` (ModelConfig.move_effects), whether each move can work on its
target (effects.py: ability, item and type immunities, and the protocol log's
earlier |-immune| / |-fail| results):

    effect_feats      f32 [12, 4, F_EFFECT]   per known move (V1's slots and move order)
    cand_effect_feats f32 [6, K, F_EFFECT]    per candidate move (cand_ids' order)

The action space and its mapping onto poke-env orders are V1's (encoder.py).
"""

from __future__ import annotations

import zlib
from typing import Dict, List, Optional, Tuple

import numpy as np
from poke_env.battle import Battle, Move, Pokemon

import effects
import encoder as E
from damage import Fighter, expected_fraction, move_order
from effects import F_EFFECT, ability_weights, neutralizing_gas, static_features
from history import (active_turns, lines_of, move_outcomes, opponent_role, previous_turn,
                     recent_events, same_move_twice, switched_by_choice)
from setpriors import MonPrior, SetPriors

A_PRIOR = 4                              # abilities / items kept per Pokémon
F_CAND = E.F_MOVE + 1
F_PRIOR = E.NT + 4
F_HIST = 8
F_EVENT = 14
F_MATCH = 16
EVENT_FLAGS = ["crit", "miss", "supereffective", "resisted", "immune", "fail", "ko"]
TYPE_INDEX = {t.name.lower(): i for i, t in enumerate(E.TYPES)}

Layout = Dict[str, Tuple[Tuple[int, ...], str]]
# The ModelConfig fields that shape the observation (encoder_spec.json's "model")
ENCODER_FLAGS = ("encoder_version", "cand_moves", "history", "history_tokens", "matchup_tokens",
                 "move_effects")
# Written to the spec only when set: the specs exported before them stay byte-identical
OPTIONAL_FLAGS = ("move_effects",)

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

    def __init__(self, priors: SetPriors, cand_moves: int = 8, history: bool = False,
                 history_tokens: int = 0, matchups: bool = False, move_effects: bool = False):
        super().__init__()
        self.matchups = matchups
        self.move_effects = move_effects
        self.priors = priors
        self.k = cand_moves
        self.history_tokens = history_tokens
        self.history = history and not history_tokens  # events replace the features
        self.layout.update({
            "cand_ids": ((E.N_SLOTS, cand_moves), "int64"),
            "cand_feats": ((E.N_SLOTS, cand_moves, F_CAND), "float32"),
            "prior_ids": ((E.N_SLOTS, 2, A_PRIOR), "int64"),
            "prior_w": ((E.N_SLOTS, 2, A_PRIOR), "float32"),
            "prior_feats": ((E.N_SLOTS, F_PRIOR), "float32"),
        })
        if self.history:
            self.layout.update({"hist_ids": ((2,), "int64"), "hist_feats": ((F_HIST,), "float32")})
        if history_tokens:
            self.layout.update({"event_ids": ((history_tokens, 2), "int64"),
                                "event_feats": ((history_tokens, F_EVENT), "float32")})
        if matchups:
            self.layout["matchup_feats"] = ((E.N_MONS, F_MATCH), "float32")
        if move_effects:
            self.layout.update({
                "effect_feats": ((E.N_MONS, E.N_MOVES, F_EFFECT), "float32"),
                "cand_effect_feats": ((E.N_SLOTS, cand_moves, F_EFFECT), "float32")})
        self._moves: Dict[str, Optional[Move]] = {}

    def _move(self, move_id: str) -> Optional[Move]:
        """A fresh (full pp) poke-env Move, cached: its features never change."""
        if move_id not in self._moves:
            self._moves[move_id] = Move(move_id, gen=9) if move_id in E._GD.moves else None
        return self._moves[move_id]

    def encode(self, battle: Battle) -> Dict[str, np.ndarray]:
        obs = E.encode(battle)
        rows = [self.priors.infer_mon(mon, self.k)
                for mon in list(battle.opponent_team.values())[:E.N_SLOTS]]
        obs.update(self.encode_priors(battle, rows))
        if self.history:
            opp = battle.opponent_active_pokemon
            active = [p for mon, p in zip(battle.opponent_team.values(), rows) if mon is opp]
            obs.update(self.encode_history(battle, bool(active) and active[0].choice_possible))
        if self.history_tokens:
            obs.update(self.encode_events(battle))
        if self.matchups:
            obs["matchup_feats"] = self.encode_matchups(battle)
        if self.move_effects:
            obs.update(self.encode_effects(battle, rows))
        return obs

    def encode_effects(self, battle: Battle, rows: List[MonPrior]) -> Dict[str, np.ndarray]:
        feats = np.zeros((E.N_MONS, E.N_MOVES, F_EFFECT), dtype=np.float32)
        cand = np.zeros((E.N_SLOTS, self.k, F_EFFECT), dtype=np.float32)
        outcomes = move_outcomes(lines_of(battle))
        gas = neutralizing_gas(battle)
        roles = (battle.player_role, opponent_role(battle))
        mine = list(battle.team.items())[:E.N_SLOTS]
        theirs = list(battle.opponent_team.items())[:E.N_SLOTS]
        names = {id(mon): key.split(": ", 1)[-1] for key, mon in mine + theirs}
        priors = {id(mon): p.abilities for (_, mon), p in zip(theirs, rows) if p.found}
        weights = lambda mon: ability_weights(mon, priors.get(id(mon)))
        actives = (battle.active_pokemon, battle.opponent_active_pokemon)
        active_weights = tuple(weights(mon) for mon in actives)

        def write(f: np.ndarray, move, mon, ours: bool) -> None:
            side, target_side = roles if ours else roles[::-1]
            target = actives[1 if ours else 0]
            f[0], f[1], f[2] = static_features(move, mon, target, weights(mon),
                                               active_weights[1 if ours else 0], gas)
            if target is not None:
                f[3] = float(outcomes.immune_before(side, move.id, target_side, names[id(target)]))
            f[4] = float(outcomes.failed_last(side, names[id(mon)], move.id, 1))
            f[5] = float(outcomes.failed_last(side, names[id(mon)], move.id, 2))

        for first, team, ours in ((0, mine, True), (E.N_SLOTS, theirs, False)):
            for i, (_, mon) in enumerate(team):
                for j, move in enumerate(list(mon.moves.values())[:E.N_MOVES]):
                    write(feats[first + i, j], move, mon, ours)
        for row, ((_, mon), p) in enumerate(zip(theirs, rows)):
            if not p.found:
                continue
            for j, (move_id, _) in enumerate(p.candidates):
                move = self._move(move_id)
                if move is not None:
                    write(cand[row, j], move, mon, False)
        return {"effect_feats": feats, "cand_effect_feats": cand}

    def encode_matchups(self, battle: Battle) -> np.ndarray:
        feats = np.zeros((E.N_MONS, F_MATCH), dtype=np.float32)
        mine = list(battle.team.values())[:E.N_SLOTS]
        theirs = list(battle.opponent_team.values())[:E.N_SLOTS]
        me, opp = battle.active_pokemon, battle.opponent_active_pokemon
        fighters: Dict[int, Fighter] = {}

        def fighter(mon, ours):
            if id(mon) not in fighters:
                fighters[id(mon)] = Fighter.of(mon, ours)
            return fighters[id(mon)]

        alive = lambda mon: mon is not None and not mon.fainted
        pairs = [(i, m, opp) for i, m in enumerate(mine)]
        pairs += [(E.N_SLOTS + j, me, t) for j, t in enumerate(theirs)]
        for row, ours, their in pairs:
            if not (alive(ours) and alive(their)):
                continue
            a, b = fighter(ours, True), fighter(their, False)
            f = feats[row]
            f[0] = 1.0
            ko = [False, False]
            for col, (att, dfn, side, owner) in enumerate([
                    (a, b, battle.opponent_side_conditions, ours),
                    (b, a, battle.side_conditions, their)]):
                for j, move in enumerate(list(owner.moves.values())[:E.N_MOVES]):
                    frac, can_ko = expected_fraction(att, dfn, move, battle, side)
                    f[1 + 4 * col + j] = frac
                    ko[col] = ko[col] or can_ko
            f[9], f[10] = f[1:5].max(), f[5:9].max()
            f[11], f[12] = float(ko[0]), float(ko[1])
            order = move_order(a, b, battle)
            f[13], f[14], f[15] = float(order == 1), float(order == 0), float(order == -1)
        return feats

    def encode_events(self, battle: Battle) -> Dict[str, np.ndarray]:
        k = self.history_tokens
        ids = np.zeros((k, 2), dtype=np.int64)
        feats = np.zeros((k, F_EVENT), dtype=np.float32)
        events = recent_events(lines_of(battle), k)
        for row, ev in zip(range(k - len(events), k), events):
            ids[row, 0] = E.MOVE_VOCAB.get(ev.move, E.UNK_ID) if ev.move else E.PAD_ID
            ids[row, 1] = E.SPECIES_VOCAB.get(ev.species, E.UNK_ID)
            f = feats[row]
            f[0] = 1.0
            f[1] = float(ev.side == battle.player_role)
            f[2] = float(ev.move is not None)
            f[3] = float(ev.move is None)
            f[4] = float(ev.drag)
            f[5] = float(ev.called)
            for i, flag in enumerate(EVENT_FLAGS):
                f[6 + i] = float(ev.flags.get(flag, False))
            f[13] = min(battle.turn - ev.turn, 10) / 10.0
        return {"event_ids": ids, "event_feats": feats}

    def encode_history(self, battle: Battle, opp_choice_possible: bool) -> Dict[str, np.ndarray]:
        me, opp = battle.active_pokemon, battle.opponent_active_pokemon
        lines = lines_of(battle)
        my_role, opp_role = battle.player_role, opponent_role(battle)
        last_turn = previous_turn(lines)
        ids = np.array([E.move_id(me.last_move if me else None),
                        E.move_id(opp.last_move if opp else None)], dtype=np.int64)
        f = np.zeros(F_HIST, dtype=np.float32)
        f[0] = min(active_turns(opp), 10) / 10.0
        f[1] = min(active_turns(me), 10) / 10.0
        f[2] = float(switched_by_choice(last_turn, opp_role))
        f[3] = float(switched_by_choice(last_turn, my_role))
        f[4] = min(opp.protect_counter, 3) / 3.0 if opp else 0.0
        f[5] = min(me.protect_counter, 3) / 3.0 if me else 0.0
        twice = same_move_twice(lines, opp_role)
        f[6] = float(twice)
        f[7] = float(twice and opp_choice_possible)
        return {"hist_ids": ids, "hist_feats": f}

    def encode_priors(self, battle: Battle, rows: List[MonPrior]) -> Dict[str, np.ndarray]:
        k = self.k
        cand_ids = np.zeros((E.N_SLOTS, k), dtype=np.int64)
        cand_feats = np.zeros((E.N_SLOTS, k, F_CAND), dtype=np.float32)
        prior_ids = np.zeros((E.N_SLOTS, 2, A_PRIOR), dtype=np.int64)
        prior_w = np.zeros((E.N_SLOTS, 2, A_PRIOR), dtype=np.float32)
        prior_feats = np.zeros((E.N_SLOTS, F_PRIOR), dtype=np.float32)
        my_active = battle.active_pokemon
        for row, (mon, p) in enumerate(zip(battle.opponent_team.values(), rows)):
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
        out = self._priors_spec()
        if self.history:
            out["history"] = {
                "features": ["opponent active turns on field (min 10) / 10",
                             "our active turns on field (min 10) / 10",
                             "opponent switched by choice in the previous turn",
                             "we switched by choice in the previous turn",
                             "opponent protect counter (min 3) / 3",
                             "our protect counter (min 3) / 3",
                             "opponent chose the same move twice in a row",
                             "that and choice_possible for the opponent's active"],
                "hist_ids": "move id of Pokemon.last_move of our active, then theirs (PAD if none)",
                "tracker_state": "poke-env Pokemon.last_move (the move flagged last used, "
                                 "flags cleared on switch-out), Pokemon._active_turns (+1 at "
                                 "every |turn|, 0 on switch-out; V1's first_turn), "
                                 "Pokemon.protect_counter",
                "protocol_log": "the battle's parsed lines in order (poke-env "
                                "Battle._replay_data, |request| excluded): the C# tracker must "
                                "keep them. previous turn = lines between the last two |turn| "
                                "lines; switched by choice = a |switch| of that side before any "
                                "|move| or |faint| of that side in the previous turn; same move "
                                "twice = the side's last two |move| lines since its last "
                                "|switch|/|drag|/|replace|, lines with a [from] tag after the "
                                "move name and Struggle skipped, have the same move name",
            }
            out["orderings"]["hist_feats"] = out["history"]["features"]
        if self.history_tokens:
            out["history_tokens"] = {
                "k": self.history_tokens,
                "layout": "the last K events, oldest first, right-aligned (padding rows first)",
                "events": "every |move| line (with or without a [from] tag) and every "
                          "|switch| / |drag| line of the protocol log (poke-env "
                          "Battle._replay_data); |replace| only updates who is who",
                "event_ids": "[move id or PAD for an entrance, species id: from the details "
                             "of the side's last entrance with that name, else the name's id]",
                "flags": "-crit / -miss / -supereffective / -resisted / -immune / -fail lines "
                         "after a |move| (until the next event) flag it, as do [miss] and "
                         "[still] / [notarget] on the |move| line; a |faint| flags KO on the "
                         "last move of the same turn by the other side, unless the fainted "
                         "side had an entrance after that move",
                "turn": "an event's turn is the last |turn| number before it (0 before turn 1); "
                        "turns ago uses poke-env's battle.turn",
            }
            out["orderings"]["event_feats"] = [
                "present", "ours", "move", "entrance", "dragged", "called_or_locked",
                *EVENT_FLAGS, "turns ago (min 10) / 10"]
        if self.move_effects:
            out["move_effects"] = effects.spec()
            out["orderings"]["effect_feats"] = effects.FEATURES
        if self.matchups:
            import damage
            out["matchup_tokens"] = {
                "rows": "0-5: (our team slot i, their active), 6-11: (our active, their "
                        "revealed slot j); 0 when either Pokémon is missing or fainted",
                "damage_calc": damage.__doc__.strip(),
            }
            out["orderings"]["matchup_feats"] = [
                "present", "ours->theirs expected fraction x4 (our move order)",
                "theirs->ours expected fraction x4 (their revealed move order)",
                "max ours->theirs", "max theirs->ours", "we can KO", "they can KO",
                "we move first", "speed tie", "they move first"]
        return out

    def _priors_spec(self) -> dict:
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
    return EncoderV2(priors, cfg.cand_moves, history=cfg.history,
                     history_tokens=cfg.history_tokens, matchups=cfg.matchup_tokens,
                     move_effects=getattr(cfg, "move_effects", False))


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
            high = E.VOCAB_SIZES["move"] if name in ("cand_ids", "hist_ids", "event_ids") \
                else E.VOCAB_SIZES["ability"]
            arr = rng.integers(0, high, size=(batch, *shape))
            if name == "prior_ids":
                arr[..., 1, :] = rng.integers(0, E.VOCAB_SIZES["item"], size=(batch, *shape[:-2], shape[-1]))
        else:
            arr = rng.random(size=(batch, *shape)).astype(np.float32)
            if name in ("cand_feats", "event_feats"):
                arr[..., 0] = (arr[..., 0] > 0.3).astype(np.float32)
        out[name] = arr.astype(np.int64 if dtype == "int64" else np.float32)
    return out
