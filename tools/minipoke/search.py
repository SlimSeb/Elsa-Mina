"""
Search at decision time: a one-turn lookahead on Showdown's simulator, scored by the
model's value head.

The model alone plays its first choice. With search, at a turn where it has two or more
legal actions:

 1. our candidates are the model's `top_k` most likely actions, whatever their
    probability (`min_prob` 0: a model sure of a bad move still gets alternatives);
 2. `search_sim.js` rebuilds the position in Showdown's simulator `determinizations`
    times, each with its own guess of the opponent's unknown sets, and plays one turn for
    each of our candidates against each of the opponent's actions (its usable moves, with
    and without tera, and switches to its revealed Pokémon), `samples` times each;
 3. each simulated turn is replayed into a copy of our poke-env battle, encoded, and
    scored like training scores it: the faint reward of the turn plus gamma x the model's
    value of the new state (+-1 when the battle ended);
 4. per determinization, the turn is a simultaneous zero-sum game: regret matching finds
    the opponent's equilibrium mix, and each of our candidates gets its expected score
    against that mix; the scores are averaged over determinizations, and we play the best
    (its policy probability breaks near ties, `prior_weight` x log probability).

Forced switches, and turns with one legal action, use the model alone, as does any turn
whose search fails. `SearchPlayer` is an `RLPlayer`: anything that plays a model can play
it with search (train.py eval / elo --search).

`depth` 2 looks one decision further where it matters most. Per determinization, the
opponent's `expand_replies` likeliest replies in its equilibrium mix (those weighing at
least `expand_min_weight`) are deepened against each of our candidates: from each of those
leaves, the next decision is played (a full turn, or the forced switches when a Pokémon
fainted), our `expand_top_k` likeliest actions there (the model's policy at the leaf, among
what Showdown allows) against each of the opponent's. The leaf then scores its turn's faint
reward plus gamma x the value of that second game (equilibrium mixes for both sides)
instead of the model's value; the turn's game is solved again with the deepened scores.
"""

from __future__ import annotations

import asyncio
import copy
import itertools
import json
import logging
import os
import re
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, NamedTuple, Optional, Sequence, Tuple

import numpy as np
from poke_env.data import GenData, to_id_str

import encoder as E
from history import active_turns
from players import FAINT_REWARD, RLPlayer

log = logging.getLogger("minipoke")
SIM_SCRIPT = Path(__file__).resolve().parent / "search_sim.js"
DURATIONS = {"reflect": 5, "lightscreen": 5, "auroraveil": 5, "tailwind": 4, "safeguard": 5,
             "mist": 5}
WEATHER_TURNS, TERRAIN_TURNS, PSEUDO_TURNS = 5, 5, 5
ENDLESS_WEATHERS = {"desolateland", "primordialsea", "deltastream"}
VOLATILES = {"SUBSTITUTE": "substitute", "LEECH_SEED": "leechseed", "CONFUSION": "confusion",
             "TAUNT": "taunt", "YAWN": "yawn", "FOCUS_ENERGY": "focusenergy", "SALT_CURE": "saltcure"}
TERRAINS = {"electricterrain", "grassyterrain", "mistyterrain", "psychicterrain"}
SPECIAL_MOVES = {"struggle", "recharge"}


@dataclass
class SearchConfig:
    top_k: int = 3               # our candidate actions, most likely first
    min_prob: float = 0.0        # ...that the policy gives at least this (the first one always)
    determinizations: int = 2    # guesses of the opponent's sets
    samples: int = 1             # simulated turns per pair of actions and determinization
    gamma: float = 0.995         # as in training
    faint_reward: float = FAINT_REWARD  # the faint reward the model was trained with
    prior_weight: float = 0.02   # x log(policy probability), added to each candidate's score
    iterations: int = 400        # regret matching iterations per game
    depth: int = 1               # 2: one more decision after the opponent's likeliest replies
    expand_top_k: int = 2        # depth 2: our candidates at that decision
    expand_replies: int = 2      # depth 2: the opponent's replies deepened per determinization...
    expand_min_weight: float = 0.1  # ...those weighing at least this in its equilibrium mix


# --------------------------------------------------------------------------
# Position: what our side knows, for search_sim.js
# --------------------------------------------------------------------------
def _name(key: str) -> str:
    return key.split(": ", 1)[-1]


def _status(status) -> str:
    return "" if status is None else status.name.lower()


def _gender(mon) -> str:
    gender = getattr(mon, "gender", None)
    return {"MALE": "M", "FEMALE": "F"}.get(getattr(gender, "name", ""), "")


def _volatiles(mon) -> List[dict]:
    return [{"id": VOLATILES[e.name]} for e in mon.effects if e.name in VOLATILES]


def _condition(text: str) -> Tuple[int, int, str]:
    """"245/300 par" -> (245, 300, "par"); "0 fnt" -> (0, 0, "fnt")."""
    hp, _, status = text.partition(" ")
    current, _, maximum = hp.partition("/")
    return int(current or 0), int(maximum or 0), status


def _ours(battle, request: dict) -> dict:
    side = request.get("side") or {}
    active_request = (request.get("active") or [{}])[0]
    active_pp = {m.get("id"): m.get("pp") for m in active_request.get("moves", [])}
    enabled = [m.get("id") for m in active_request.get("moves", []) if not m.get("disabled")]
    pokemon = []
    for p in side.get("pokemon", []):
        mon = battle.team.get(p["ident"])
        details = p["details"].split(", ")
        level = next((int(d[1:]) for d in details if re.fullmatch(r"L\d+", d)), 100)
        gender = next((d for d in details if d in ("M", "F")), "")
        hp, maxhp, status = _condition(p.get("condition", "0 fnt"))
        moves = [{"id": m, "pp": active_pp.get(m) if p.get("active") else None} for m in p.get("moves", [])]
        entry = {
            "name": _name(p["ident"]), "species": details[0], "level": level, "gender": gender,
            "moves": moves, "item": p.get("item", ""), "ability": p.get("ability") or p.get("baseAbility"),
            "teraType": p.get("teraType"), "terastallized": p.get("terastallized") or None,
            "hp": hp, "maxhp": maxhp, "status": "" if status == "fnt" else status,
            "fainted": status == "fnt", "stats": p.get("stats"), "active": bool(p.get("active")),
        }
        if mon is not None and p.get("active"):
            entry["boosts"] = {k: v for k, v in mon.boosts.items() if v}
            entry["activeTurns"] = active_turns(mon)
            entry["volatiles"] = _volatiles(mon)
            item = p.get("item", "")
            if item.startswith("choice") and len(enabled) == 1 and len(active_request.get("moves", [])) > 1:
                entry["volatiles"].append({"id": "choicelock", "move": enabled[0]})
        pokemon.append(entry)
    return {"pokemon": pokemon, "size": len(pokemon), "teraUsed": bool(battle.used_tera),
            "conditions": _conditions(battle.side_conditions, battle.turn)}


def _theirs(battle) -> dict:
    pokemon = []
    for key, mon in battle.opponent_team.items():
        item = mon.item
        pokemon.append({
            "name": _name(key), "species": mon.species, "forme": mon.species, "level": mon.level,
            "gender": _gender(mon),
            "moves": [{"id": m.id, "pp": m.current_pp} for m in list(mon.moves.values())[:E.N_MOVES]
                      if m.id not in SPECIAL_MOVES],
            "ability": mon.ability,
            "item": None if item == GenData.UNKNOWN_ITEM else (item or ""),
            "teraType": mon.tera_type.name.lower() if mon.is_terastallized and mon.tera_type else None,
            "terastallized": mon.tera_type.name.capitalize() if mon.is_terastallized and mon.tera_type else None,
            "hpFraction": mon.current_hp_fraction, "status": "" if mon.fainted else _status(mon.status),
            "fainted": mon.fainted, "active": bool(mon.active),
            "boosts": {k: v for k, v in mon.boosts.items() if v} if mon.active else {},
            "activeTurns": active_turns(mon) if mon.active else 0,
            "volatiles": _volatiles(mon) if mon.active else [],
        })
    role = "p2" if battle.player_role == "p1" else "p1"
    size = getattr(battle, "_team_size", {}).get(role, 6)
    return {"pokemon": pokemon, "size": max(size, len(pokemon)), "teraUsed": bool(battle.opponent_used_tera),
            "conditions": _conditions(battle.opponent_side_conditions, battle.turn)}


def _conditions(conditions: dict, turn: int) -> Dict[str, int]:
    out = {}
    for condition, value in conditions.items():
        cid = condition.name.lower().replace("_", "")
        if cid in ("spikes", "toxicspikes"):
            out[cid] = int(value)
        elif cid in DURATIONS:
            out[cid] = max(1, DURATIONS[cid] - (turn - int(value)))
        else:
            out[cid] = 1
    return out


def _field(battle) -> dict:
    out: dict = {"pseudo": {}}
    for weather, start in battle.weather.items():
        wid = weather.name.lower()
        out["weather"] = wid
        out["weatherTurns"] = 0 if wid in ENDLESS_WEATHERS else max(1, WEATHER_TURNS - (battle.turn - start))
    for fld, start in battle.fields.items():
        fid = fld.name.lower().replace("_", "")
        turns = max(1, (TERRAIN_TURNS if fid in TERRAINS else PSEUDO_TURNS) - (battle.turn - start))
        if fid in TERRAINS:
            out["terrain"], out["terrainTurns"] = fid, turns
        elif fid in ("trickroom", "gravity", "magicroom", "wonderroom"):
            out["pseudo"][fid] = turns
    return out


def position_of(battle) -> dict:
    """What our side knows of the battle, for search_sim.js."""
    us = battle.player_role
    them = "p2" if us == "p1" else "p1"
    return {"us": us, "turn": battle.turn, "field": _field(battle),
            "sides": {us: _ours(battle, battle.last_request or {}), them: _theirs(battle)}}


def sim_action(action: int, battle) -> Optional[dict]:
    """A compact action as search_sim.js plays it: a switch by name, a move by id."""
    if action < 6:
        team = list(battle.team)
        return {"kind": "switch", "name": _name(team[action])} if action < len(team) else None
    moves = E.move_options(battle)
    j = (action - 6) % E.N_MOVES
    if j >= len(moves) or moves[j].id in SPECIAL_MOVES:
        return None
    return {"kind": "move", "move": moves[j].id, "tera": action >= 10}


def leaf_candidates(leaf, choices: dict, probs: np.ndarray, top_k: int) -> Optional[List[dict]]:
    """Our candidates at the decision after a simulated turn: the `top_k` likeliest actions
    of the model's policy at the leaf among what Showdown allows there (`choices`, from
    search_sim.js); [] when we only wait for the opponent's forced switch, None when none of
    the model's actions is allowed."""
    if choices.get("wait"):
        return []
    moves, switches = set(choices.get("moves") or []), set(choices.get("switches") or [])
    out: List[dict] = []
    for action in np.argsort(-probs, kind="stable"):
        act = sim_action(int(action), leaf)
        if act is None or act in out:
            continue
        if act["kind"] == "switch":
            allowed = act["name"] in switches
        else:
            allowed = act["move"] in moves and (not act["tera"] or bool(choices.get("tera")))
        if allowed:
            out.append(act)
            if len(out) == top_k:
                break
    return out or None


# --------------------------------------------------------------------------
# The simulator processes
# --------------------------------------------------------------------------
class SimPool:
    """`workers` Node processes running search_sim.js, created on the loop that first uses
    them (poke-env's)."""

    def __init__(self, showdown_cwd: str | Path, workers: int = 2):
        self.showdown = str(Path(showdown_cwd).expanduser().resolve())
        self.workers = workers
        self._idle: Optional[asyncio.Queue] = None
        self._procs: List[asyncio.subprocess.Process] = []
        self._ids = itertools.count(1)

    async def _start(self) -> None:
        self._idle = asyncio.Queue()
        for _ in range(self.workers):
            proc = await asyncio.create_subprocess_exec(
                "node", str(SIM_SCRIPT), self.showdown, stdin=asyncio.subprocess.PIPE,
                stdout=asyncio.subprocess.PIPE, limit=1 << 28)
            self._procs.append(proc)
            self._idle.put_nowait(proc)

    async def simulate(self, request: dict) -> dict:
        if self._idle is None:
            await self._start()
        proc = await self._idle.get()
        try:
            request = {**request, "id": next(self._ids)}
            proc.stdin.write((json.dumps(request) + "\n").encode())
            await proc.stdin.drain()
            reply = json.loads(await proc.stdout.readline())
        finally:
            self._idle.put_nowait(proc)
        if "error" in reply:
            raise RuntimeError(reply["error"][:500])
        return reply

    def close(self) -> None:
        for proc in self._procs:
            if proc.returncode is None:
                proc.kill()
        self._procs.clear()

    async def aclose(self) -> None:
        """close(), waiting for the processes to exit (from the loop that started them)."""
        procs = list(self._procs)
        self.close()
        for proc in procs:
            await proc.wait()


# --------------------------------------------------------------------------
# Scoring
# --------------------------------------------------------------------------
def leaf_battle(battle, lines: Sequence[str]):
    """Our battle after the simulated turn: a copy with its lines applied (one poke-env
    can't parse is skipped, as the live player does)."""
    leaf = copy.deepcopy(battle)
    for line in lines:
        try:
            leaf.parse_message(line.split("|"))
        except Exception:  # noqa: BLE001
            pass
    me = leaf.active_pokemon
    leaf._force_switch = bool(me is not None and me.fainted)
    leaf._trapped = False
    if leaf.used_tera:
        leaf._can_tera = False
    return leaf


def solve_zero_sum(payoff: np.ndarray, iterations: int = 400) -> Tuple[np.ndarray, np.ndarray]:
    """Equilibrium mixes (ours, theirs) of a zero-sum game where we maximize `payoff`
    [ours x theirs], by regret matching+ (the averages of the iterates)."""
    rows, cols = payoff.shape
    regret_r, regret_c = np.zeros(rows), np.zeros(cols)
    sum_r, sum_c = np.zeros(rows), np.zeros(cols)
    for t in range(1, iterations + 1):
        x = regret_r / regret_r.sum() if regret_r.sum() > 0 else np.full(rows, 1 / rows)
        y = regret_c / regret_c.sum() if regret_c.sum() > 0 else np.full(cols, 1 / cols)
        sum_r += t * x
        sum_c += t * y
        row_values = payoff @ y
        col_values = -(x @ payoff)
        regret_r = np.maximum(regret_r + row_values - x @ row_values, 0)
        regret_c = np.maximum(regret_c + col_values - y @ col_values, 0)
    return sum_r / sum_r.sum(), sum_c / sum_c.sum()


class Scored(NamedTuple):
    """A simulated leaf's score: +-1 when it ended the battle, else `reward` (its faint
    reward) plus gamma x the model's value of `battle` (its poke-env copy); `probs` the
    model's policy there, over every action when it was asked for unmasked."""
    score: float
    reward: float
    battle: object
    probs: Optional[np.ndarray]


@dataclass
class SearchStats:
    searched: int = 0
    changed: int = 0
    failed: int = 0
    seconds: float = 0.0
    leaves: int = 0
    expanded: int = 0

    def describe(self) -> str:
        n = max(self.searched, 1)
        deeper = f", {self.expanded / n:.1f} leaves deepened" if self.expanded else ""
        return (f"searched {self.searched} decisions ({self.changed} changed, {self.failed} failed), "
                f"{self.leaves / n:.0f} leaves{deeper} and {self.seconds / n:.2f}s each")


class SearchPlayer(RLPlayer):
    """An RLPlayer that searches one turn ahead before playing (see the module docstring)."""

    def __init__(self, batcher, sims: SimPool, config: Optional[SearchConfig] = None, **kwargs):
        super().__init__(batcher, None, greedy=True, **kwargs)
        self.sims = sims
        self.config = config or SearchConfig()
        self.stats = SearchStats()
        self._seed = itertools.count(int(time.time()) % 100000)

    async def _choose(self, battle):
        obs = self.batcher.encoder.encode(battle)
        legal = np.flatnonzero(obs["mask"])
        if len(legal) < 2 or battle.force_switch or battle.active_pokemon is None:
            return await super()._choose(battle)
        action, _, _, probs = await self.batcher.act(obs, True)
        try:
            chosen = await self._search(battle, legal, probs)
        except Exception as exc:  # noqa: BLE001
            self.stats.failed += 1
            if self.stats.failed <= 5:
                log.warning("search failed in %s: %s", battle.battle_tag, str(exc)[:300])
            chosen = None
        if chosen is None:
            chosen = action
        self.stats.changed += int(chosen != action)
        return E.action_to_order(chosen, battle)

    async def _search(self, battle, legal: np.ndarray, probs: np.ndarray) -> Optional[int]:
        cfg = self.config
        t0 = time.perf_counter()
        ranked = sorted(legal, key=lambda a: -probs[a])
        candidates = [int(a) for a in ranked[:cfg.top_k] if probs[a] >= cfg.min_prob or a == ranked[0]]
        actions = [sim_action(a, battle) for a in candidates]
        keep = [i for i, act in enumerate(actions) if act is not None]
        if len(keep) < 2:
            return None
        candidates = [candidates[i] for i in keep]
        actions = [actions[i] for i in keep]
        request = {"position": position_of(battle), "ours": actions, "determinizations": cfg.determinizations,
                   "samples": cfg.samples, "seed": next(self._seed)}
        reply = await self.sims.simulate(request)
        dets = reply["determinizations"]
        keys = [(d, a, b, s) for d, det in enumerate(dets) for a, row in enumerate(det["leaves"])
                for b, samples in enumerate(row) for s, leaf in enumerate(samples) if leaf is not None]
        leaves = [dets[d]["leaves"][a][b][s] for d, a, b, s in keys]
        scored = dict(zip(keys, await self._score([(battle, leaf) for leaf in leaves], unmasked=cfg.depth >= 2)))
        totals = {key: leaf_score.score for key, leaf_score in scored.items()}
        self.stats.leaves += len(leaves)
        if cfg.depth >= 2:
            await self._deepen(request, dets, scored, totals, len(candidates))
        per_det = [q for *_, q in self._games(dets, totals, len(candidates))]
        if not per_det:
            return None
        stacked = np.vstack(per_det)
        valid = ~np.isnan(stacked).all(axis=0)
        scores = np.full(len(candidates), np.nan)
        scores[valid] = np.nanmean(stacked[:, valid], axis=0)
        scores += cfg.prior_weight * np.log(np.maximum(probs[candidates], 1e-6))
        self.stats.searched += 1
        self.stats.seconds += time.perf_counter() - t0
        if not np.isfinite(scores).any():
            return None
        return candidates[int(np.nanargmax(scores))]

    async def _score(self, pairs: Sequence[Tuple[object, dict]], unmasked: bool = False) -> List[Scored]:
        """Each (parent battle, simulated leaf after it) scored like training scores a turn
        (see Scored), the model's values in one batch. The leaf's mask is the last request's,
        stale after a simulated turn: `unmasked` asks the model's policy over every action."""
        cfg = self.config
        battles = [leaf_battle(parent, leaf["lines"]) if leaf["winner"] is None else None
                   for parent, leaf in pairs]
        observations = []
        for leaf_battle_ in battles:
            if leaf_battle_ is not None:
                obs = self.batcher.encoder.encode(leaf_battle_)
                if unmasked:
                    obs["mask"] = np.ones_like(obs["mask"])
                observations.append(obs)
        evaluated = iter(await asyncio.gather(*[self.batcher.act(o, True) for o in observations]))
        out = []
        for (parent, leaf), leaf_battle_ in zip(pairs, battles):
            if leaf["winner"] is not None:
                us = parent.player_role
                score = 0.0 if leaf["winner"] == "tie" else (1.0 if leaf["winner"] == us else -1.0)
                out.append(Scored(score, score, None, None))
                continue
            _, _, value, leaf_probs = next(evaluated)
            mine0, theirs0 = E.fainted_counts(parent)
            mine, theirs = E.fainted_counts(leaf_battle_)
            reward = cfg.faint_reward * ((theirs - theirs0) - (mine - mine0))
            out.append(Scored(reward + cfg.gamma * value, reward, leaf_battle_, leaf_probs))
        return out

    def _games(self, dets: List[dict], totals: Dict[Tuple[int, int, int, int], float], n_candidates: int):
        """Per determinization with a game: (d, its rows, its columns, the opponent's
        equilibrium mix over those columns, each candidate's expected score against it)."""
        out = []
        for d, det in enumerate(dets):
            n_theirs = len(det["theirs"])
            if n_theirs == 0:
                continue
            sums, counts = np.zeros((n_candidates, n_theirs)), np.zeros((n_candidates, n_theirs))
            for (dd, a, b, _), score in totals.items():
                if dd == d:
                    sums[a, b] += score
                    counts[a, b] += 1
            payoff = np.where(counts > 0, sums / np.maximum(counts, 1), np.nan)
            game, rows, cols = _playable(payoff)
            if game is None:
                continue
            _, theirs_mix = solve_zero_sum(game, self.config.iterations)
            q = np.full(n_candidates, np.nan)
            q[rows] = game @ theirs_mix
            out.append((d, rows, cols, theirs_mix, q))
        return out

    async def _deepen(self, request: dict, dets: List[dict], scored: Dict[tuple, Scored],
                      totals: Dict[tuple, float], n_candidates: int) -> None:
        """Depth 2 (see the module docstring): replaces the scores of the leaves after the
        opponent's likeliest replies with their turn's reward plus gamma x the value of the
        next decision."""
        cfg = self.config
        items, parents = [], []
        for d, rows, cols, theirs_mix, _ in self._games(dets, totals, n_candidates):
            order = np.argsort(-theirs_mix, kind="stable")[:cfg.expand_replies]
            replies = [int(cols[j]) for j in order if theirs_mix[j] >= cfg.expand_min_weight]
            for a in (int(row) for row in rows):
                for b in replies:
                    for s, leaf in enumerate(dets[d]["leaves"][a][b]):
                        parent = scored.get((d, a, b, s))
                        if parent is None or parent.battle is None or not leaf.get("choices"):
                            continue
                        ours = leaf_candidates(parent.battle, leaf["choices"], parent.probs, cfg.expand_top_k)
                        if ours is None:
                            continue
                        items.append({"d": d, "a": a, "b": b, "s": s, "ours": ours})
                        parents.append(((d, a, b, s), leaf, parent))
        if not items:
            return
        reply = await self.sims.simulate({**request, "expand": items})
        expansions = [(key, parent, expansion) for (key, leaf, parent), expansion
                      in zip(parents, reply["expansions"])
                      if expansion is not None and expansion["first"] == leaf["lines"]]
        where, pairs = [], []
        for e, (_, parent, expansion) in enumerate(expansions):
            for r, row in enumerate(expansion["leaves"]):
                for c, samples in enumerate(row):
                    for leaf in samples:
                        if leaf is not None:
                            where.append((e, r, c))
                            pairs.append((parent.battle, leaf))
        values: Dict[Tuple[int, int, int], List[float]] = {}
        for (e, r, c), leaf_score in zip(where, await self._score(pairs)):
            values.setdefault((e, r, c), []).append(leaf_score.score)
        self.stats.leaves += len(pairs)
        for e, (key, parent, expansion) in enumerate(expansions):
            payoff = np.full((len(expansion["leaves"]), len(expansion["theirs"])), np.nan)
            for (ee, r, c), scores in values.items():
                if ee == e:
                    payoff[r, c] = float(np.mean(scores))
            game, _, _ = _playable(payoff)
            if game is None:
                continue
            ours_mix, theirs_mix = solve_zero_sum(game, cfg.iterations)
            totals[key] = parent.reward + cfg.gamma * float(ours_mix @ game @ theirs_mix)
            self.stats.expanded += 1


def _playable(payoff: np.ndarray):
    """The game left once rows without any score, then columns missing one, are dropped:
    (game, its rows, its columns), or (None, None, None)."""
    rows = np.flatnonzero(~np.isnan(payoff).all(axis=1))
    if len(rows) == 0:
        return None, None, None
    cols = np.flatnonzero(~np.isnan(payoff[rows]).any(axis=0))
    if len(cols) == 0:
        return None, None, None
    return payoff[np.ix_(rows, cols)], rows, cols
