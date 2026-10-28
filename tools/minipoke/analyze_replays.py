"""
Analyze the ladder battles ElsaMina saved with -savereplays: how a minipoke model
plays against humans (results, rating, value head, behavior, bugs), and whether
ElsaMina's C# tracker and encoder still match poke-env on real games.

    python analyze_replays.py replays/replays --out replays/analysis

The replay folder holds two files per battle, named after the replay id
(gen9randombattle-<n>). It is only read, never written.

    <id>.log    every line the bot received in the battle room, first person, our
                |request| lines included (ReplaySavingService.cs); it stops at
                |win| / |tie|
    <id>.json   Showdown's public replay: players, rating, "log" (spectator view,
                ending with the rating changes) and "inputlog" (every choice of
                both sides: ">p2 move icebeam", ">p2 move knockoff terastallize",
                ">p2 switch 3", 3 being a position in that request's side.pokemon)

Pipeline:
 1. pair and parse the files: sides, ratings before and after, outcome, how it
    ended, turns, Pokémon left, start time. A broken pair is reported and skipped.
 2. replay each .log into poke-env 0.16.1 through a real Player
    (Player._handle_battle_message, offline, one line at a time like
    make_fixtures.RecordingPlayer): every choose_move call is a decision point,
    encoded right there with each model's encoder (encoder_v2.py).
 3. align the decision points with our inputlog choices (a choice Showdown
    rejected with an |error| is asked again; the last request may never be
    answered when the opponent leaves first) and map each choice to its compact
    action (encoder.py: 0-5 switch to battle.team slot i, 6-9 move j of the
    moves poke-env's move actions stand for, 10-13 tera + move j).
 4. run each model's minipoke.onnx on the decision points. ElsaMina plays the
    argmax of the legal logits, so for the model it played with the argmax should
    be our choice: a parity check of the C# tracker and encoder against poke-env.
    Which model played a battle is read from its choices (attribute_models): the
    strategy is switched by hand, and a folder can hold several models' battles.
    ElsaMina's guard (MiniPokeActionGuard.cs, guard_vetoes here) replaces an argmax
    it vetoes by the best action left; whether a battle was played with it is read
    from its choices too. So is the search (MiniPokeTurnSearch.cs, search.py): it
    plays one of the model's likely actions, not always the argmax, and can't be
    replayed (its simulations are random), so a battle played with it is checked
    against its candidates (search_candidates).
 5. write to --out (results, value head and behavior: PLAYED_BY's battles only):

    report.md         findings, results, endings, parity, value head, behavior
    charts/*.png
    battles.csv       one row per battle
    decisions.jsonl   one line per decision point: probabilities, value, entropy,
                      choice, legal mask, turn, state (and the comparison model's)
    value_drops.csv   for each loss, the decision where the value dropped the most
    value_calibration.json  the affine map from value to realized return (ElsaMina
                      reads it next to the model: --write-calibration puts it there)
    cache/            the replayed decision points and their encodings, one .npz
                      per battle: "meta" (JSON: the decisions in order, their
                      "index" in decisions.jsonl) and "<encoder key>__<input>"
                      arrays [decisions, ...]. Reused while the log, poke-env and
                      the encoders are the same; --refresh replays everything.

Live, poke-env drops the rest of a websocket frame after a line it raises on. The
logs keep no frame boundaries, so such a line is skipped alone (as ElsaMina's
BattleLogTracker does) and reported under data issues.
"""

from __future__ import annotations

import argparse
import asyncio
import csv
import hashlib
import json
import logging
import math
import re
import time
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from datetime import datetime
from importlib.metadata import version
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple

import numpy as np
from poke_env import AccountConfiguration
from poke_env.concurrency import POKE_LOOP
from poke_env.data import GenData, to_id_str
from poke_env.player import Player

import encoder as E

FORMAT = "gen9randombattle"
REPLAY_URL = "https://replay.pokemonshowdown.com/"
REPO_ROOT = Path(__file__).resolve().parents[2]
MODELS_DIR = REPO_ROOT / "src" / "ElsaMina.Battles" / "Models"
PLAYED_BY = "MiniPokeV2_1M"     # the model the report is about
COMPARE_WITH = "MiniPokeV2_4M"  # run on the same states (step 6), and a candidate for attribution
CACHE_VERSION = 1

# The value head's return: players.Rewards "shaped" (what the v2 runs trained with,
# faint reward 0.1 throughout) discounted by ppo.PPOConfig.gamma per decision
WIN_REWARD = 1.0
FAINT_REWARD = 0.1
GAMMA = 0.995

TIE_TOLERANCE = 1e-5        # a logit this close to the best one ties with it
NEAR_TIE_LOGITS = 0.05      # mismatches closer than this are float noise candidates
LOW_HP = 1 / 3
EARLY_FORFEIT_TURN = 3      # an opponent forfeit before this turn is not a game
RATING_EDGES = [1050, 1100, 1150, 1200, 1250]
TURN_EDGES = [3, 6, 10, 15, 20, 30]
VALUE_EDGES = [-0.75, -0.5, -0.25, 0.0, 0.25, 0.5, 0.75]
WORST = 5                   # battles listed for each behavior


# --------------------------------------------------------------------------
# 1. Files
# --------------------------------------------------------------------------
def to_id(name: Optional[str]) -> str:
    return to_id_str(name or "")


def replay_url(battle_id: str) -> str:
    return REPLAY_URL + battle_id


def read_lines(path: Path) -> List[str]:
    lines = path.read_text(encoding="utf-8").split("\n")
    return lines[:-1] if lines and lines[-1] == "" else lines


@dataclass
class Choice:
    """One choice of an inputlog: ">p2 move knockoff terastallize", ">p1 switch 3"."""
    side: str
    kind: str                       # "move", "switch", or the inputlog's word for anything else
    move: Optional[str] = None      # move id
    tera: bool = False
    position: Optional[int] = None  # switch: 1-based position in that request's side.pokemon
    raw: str = ""


CHOICE_RE = re.compile(r"^>(p[1-4]) (\S+)(?: (.*))?$")


def parse_choice(line: str) -> Optional[Choice]:
    match = CHOICE_RE.match(line)
    if match is None:
        return None
    side, kind = match.group(1), match.group(2)
    words = (match.group(3) or "").split()
    choice = Choice(side, kind, raw=line[1:])
    if kind == "move" and words:
        choice.move = to_id(words[0])
        choice.tera = "terastallize" in words[1:]
    elif kind == "switch" and words and words[0].isdigit():
        choice.position = int(words[0])
    return choice


@dataclass
class ReplayInfo:
    """What the public replay (.json) adds to the log."""
    id: str
    formatid: str
    upload_time: Optional[int]
    rating: Optional[int]
    players: Dict[str, dict]                    # side -> the inputlog's ">player" JSON
    choices: List[Choice]
    forcelose: List[str]                        # sides
    rating_changes: Dict[str, Tuple[int, int]]  # name id -> (before, after)

    def choices_of(self, side: str) -> List[Choice]:
        return [c for c in self.choices if c.side == side]


RATING_RE = re.compile(r"^(.*)'s rating: (\d+) &rarr; <strong>(\d+)</strong>")


def parse_replay(data: dict) -> ReplayInfo:
    players: Dict[str, dict] = {}
    choices: List[Choice] = []
    forcelose: List[str] = []
    for line in (data.get("inputlog") or "").split("\n"):
        if line.startswith(">player "):
            _, side, payload = line.split(" ", 2)
            players[side] = json.loads(payload)
        elif line.startswith(">forcelose "):
            forcelose.append(line.split(" ", 1)[1].strip())
        else:
            choice = parse_choice(line)
            if choice is not None:
                choices.append(choice)
    changes = {}
    for line in (data.get("log") or "").split("\n"):
        if line.startswith("|raw|"):
            match = RATING_RE.match(line[len("|raw|"):])
            if match:
                changes[to_id(match.group(1))] = (int(match.group(2)), int(match.group(3)))
    return ReplayInfo(id=data.get("id", ""), formatid=data.get("formatid", ""),
                      upload_time=data.get("uploadtime"), rating=data.get("rating"),
                      players=players, choices=choices, forcelose=forcelose,
                      rating_changes=changes)


FORFEIT_RE = re.compile(r"^(.+) forfeited\.$")
INACTIVE_RE = re.compile(r"^(.+) lost due to inactivity\.$")


@dataclass
class LogInfo:
    """What the bot's own log (.log) says about a battle."""
    our_side: Optional[str] = None
    our_name: Optional[str] = None
    names: Dict[str, str] = field(default_factory=dict)            # side -> name (|player|)
    ratings: Dict[str, Optional[int]] = field(default_factory=dict)  # side -> rating before
    winner: Optional[str] = None
    tie: bool = False
    turns: int = 0
    start_time: Optional[int] = None
    end_time: Optional[int] = None
    team_size: Dict[str, int] = field(default_factory=dict)
    fainted: Counter = field(default_factory=Counter)
    forfeited_name: Optional[str] = None
    inactive_name: Optional[str] = None
    errors: List[Tuple[int, str]] = field(default_factory=list)    # (line index, text)
    requests: int = 0

    @property
    def opp_side(self) -> Optional[str]:
        return None if self.our_side is None else ("p2" if self.our_side == "p1" else "p1")

    def side_named(self, name: Optional[str]) -> Optional[str]:
        wanted = to_id(name)
        return next((side for side, n in self.names.items() if to_id(n) == wanted), None)

    def mons_left(self, side: str) -> int:
        return self.team_size.get(side, 6) - self.fainted[side]

    @property
    def outcome(self) -> str:
        if self.tie:
            return "tie"
        if self.winner is None:
            return "none"
        return "win" if self.side_named(self.winner) == self.our_side else "loss"

    @property
    def ending(self) -> Tuple[str, Optional[str]]:
        """How it ended and the side it ended for: ("forfeit", side that forfeited),
        ("inactivity", side that timed out), ("all fainted", side left without
        Pokémon), ("tie", None), ("no result", None), or ("other", loser)."""
        if self.tie:
            return "tie", None
        if self.forfeited_name is not None:
            return "forfeit", self.side_named(self.forfeited_name)
        if self.inactive_name is not None:
            return "inactivity", self.side_named(self.inactive_name)
        if self.winner is None:
            return "no result", None
        winner = self.side_named(self.winner)
        loser = None if winner is None else ("p2" if winner == "p1" else "p1")
        if loser is not None and self.mons_left(loser) <= 0:
            return "all fainted", loser
        return "other", loser


def parse_log(lines: Sequence[str]) -> LogInfo:
    info = LogInfo()
    for i, line in enumerate(lines):
        parts = line.split("|")
        if len(parts) < 2:
            continue
        kind = parts[1]
        if kind == "request" and len(parts) > 2 and parts[2] not in ("", "null"):
            info.requests += 1
            if info.our_side is None:
                side = json.loads(parts[2]).get("side") or {}
                info.our_side, info.our_name = side.get("id"), side.get("name")
        elif kind == "player" and len(parts) > 3 and parts[3]:
            info.names.setdefault(parts[2], parts[3])
            rating = parts[5] if len(parts) > 5 else ""
            if rating.isdigit() and info.ratings.get(parts[2]) is None:
                info.ratings[parts[2]] = int(rating)
        elif kind == "t:" and len(parts) > 2 and parts[2].isdigit():
            info.end_time = int(parts[2])
            info.start_time = info.start_time or info.end_time
        elif kind == "turn" and len(parts) > 2:
            info.turns = int(parts[2])
        elif kind == "teamsize" and len(parts) > 3:
            info.team_size[parts[2]] = int(parts[3])
        elif kind == "faint" and len(parts) > 2:
            info.fainted[parts[2][:2]] += 1
        elif kind == "-heal" and "[from] move: Revival Blessing" in parts[4:]:
            info.fainted[parts[2][:2]] -= 1
        elif kind == "-message" and len(parts) > 2:
            if (match := FORFEIT_RE.match(parts[2])) is not None:
                info.forfeited_name = match.group(1)
            elif (match := INACTIVE_RE.match(parts[2])) is not None:
                info.inactive_name = match.group(1)
        elif kind == "win" and len(parts) > 2:
            info.winner = parts[2]
        elif kind == "tie":
            info.tie = True
        elif kind == "error":
            info.errors.append((i, "|".join(parts[2:])))
    return info


class BrokenPair(Exception):
    pass


def pair_files(folder: Path) -> Tuple[List[str], List[str]]:
    """The replay ids with both files, and a line for each file without its pair."""
    logs = {p.stem for p in folder.glob("*.log")}
    jsons = {p.stem for p in folder.glob("*.json")}
    issues = [f"`{stem}.log` has no `.json`" for stem in sorted(logs - jsons)]
    issues += [f"`{stem}.json` has no `.log`" for stem in sorted(jsons - logs)]
    return sorted(logs & jsons), issues


def load_pair(folder: Path, stem: str) -> Tuple[List[str], LogInfo, ReplayInfo]:
    try:
        lines = read_lines(folder / f"{stem}.log")
    except (OSError, UnicodeDecodeError) as exc:
        raise BrokenPair(f"unreadable .log ({exc})") from exc
    try:
        data = json.loads((folder / f"{stem}.json").read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise BrokenPair(f"unreadable .json ({exc})") from exc
    if not isinstance(data, dict) or "inputlog" not in data:
        raise BrokenPair(".json has no inputlog")
    if not lines or not lines[0].startswith("|init|battle"):
        raise BrokenPair(".log does not start with |init|battle")
    try:
        log, replay = parse_log(lines), parse_replay(data)
    except (ValueError, KeyError, IndexError) as exc:
        raise BrokenPair(f"unparsable ({exc})") from exc
    if log.our_side is None:
        raise BrokenPair(".log has no |request| (not a battle the bot played)")
    if replay.formatid != FORMAT:
        raise BrokenPair(f"format {replay.formatid}, not {FORMAT}")
    if to_id(replay.players.get(log.our_side, {}).get("name")) != to_id(log.our_name):
        raise BrokenPair("the .json's players differ from the .log's")
    return lines, log, replay


# --------------------------------------------------------------------------
# 2. Replay into poke-env
# --------------------------------------------------------------------------
async def _send_nothing(*args, **kwargs) -> None:
    return None


class _KeepMessages(logging.Handler):
    def __init__(self, out: List[str]):
        super().__init__(25)
        self.out = out

    def emit(self, record: logging.LogRecord) -> None:
        self.out.append(record.getMessage()[:160])


class ReplayPlayer(Player):
    """poke-env's Player, offline: replay() feeds it a saved battle log one line at a
    time through Player._handle_battle_message, as make_fixtures.RecordingPlayer feeds
    live frames. choose_move records the decision point and sends nothing."""

    def __init__(self, username: str, encoders: Dict[str, object]):
        # max_concurrent_battles=0: an unbounded battle count queue
        super().__init__(account_configuration=AccountConfiguration(username, None),
                         battle_format=FORMAT, start_listening=False, max_concurrent_battles=0)
        self.ps_client.send_message = _send_nothing
        self.encoders = encoders
        self.decisions: List[dict] = []
        self.messages: List[str] = []
        self._line = -1
        # Every PSClient adds a stream handler to the logger named after the user:
        # keep poke-env's warnings (and its level-25 errors) for the report instead
        self.logger.handlers.clear()
        self.logger.propagate = False
        self.logger.setLevel(25)
        self.logger.addHandler(_KeepMessages(self.messages))

    async def replay(self, tag: str, lines: Sequence[str]) -> List[Tuple[int, str]]:
        header = [f">{tag}"]
        errors = []
        for i, line in enumerate(lines):
            self._line = i
            try:
                await self._handle_battle_message([header, line.split("|")])
            except Exception as exc:  # noqa: BLE001 - reported, like PSClient logs it
                errors.append((i, f"{type(exc).__name__}: {exc}"[:200]))
        return errors

    def choose_move(self, battle):
        self.decisions.append(snapshot(battle, self._line, self.encoders))
        return self.choose_default_move()


move_options = E.move_options


def describe_mon(mon) -> str:
    if mon is None:
        return "?"
    status = mon.status.name.lower() if mon.status is not None and mon.status.name != "FNT" else ""
    extras = "".join(f" {x}" for x in (status, "tera" if mon.is_terastallized else "") if x)
    name = (E._GD.pokedex.get(mon.species) or {}).get("name", mon.species)
    return f"{name} {round(100 * mon.current_hp_fraction)}%{extras}"


def snapshot(battle, line: int, encoders: Dict[str, object]) -> dict:
    """Everything kept about a decision point (the encodings go to the cache)."""
    request = battle.last_request or {}
    mine, theirs = E.fainted_counts(battle)
    return {
        "line": line,
        "turn": battle.turn,
        "rqid": request.get("rqid"),
        "force_switch": bool(battle.force_switch),
        "trapped": bool(battle.trapped),
        "can_tera": bool(battle.can_tera),
        "team": list(battle.team),
        "request_team": [p["ident"] for p in (request.get("side") or {}).get("pokemon", [])],
        "moves": [m.id for m in move_options(battle)],
        "fainted": [mine, theirs],
        "state": f"{describe_mon(battle.active_pokemon)} vs "
                 f"{describe_mon(battle.opponent_active_pokemon)} "
                 f"({6 - mine}-{6 - theirs} left)",
        "obs": {key: enc.encode(battle) for key, enc in encoders.items()},
    }


def stack(encoder, observations: List[dict]) -> Dict[str, np.ndarray]:
    out = {}
    for name, (shape, dtype) in encoder.layout.items():
        out[name] = (np.stack([o[name] for o in observations]) if observations
                     else np.zeros((0, *shape), dtype=dtype))
    return out


def replay_battle(stem: str, lines: Sequence[str], username: str,
                  encoders: Dict[str, object]) -> dict:
    player = ReplayPlayer(username, encoders)
    tag = f"battle-{stem}"
    errors = asyncio.run_coroutine_threadsafe(player.replay(tag, lines), POKE_LOOP).result()
    battle = player.battles.get(tag)
    observations = [d.pop("obs") for d in player.decisions]
    return {
        "decisions": player.decisions,
        "obs": {key: stack(enc, [o[key] for o in observations]) for key, enc in encoders.items()},
        "parse_errors": [list(e) for e in errors],
        "messages": player.messages,
        "final_fainted": list(E.fainted_counts(battle)) if battle is not None else [0, 0],
    }


def cache_folder(out: Path, keys: Sequence[str]) -> Path:
    digest = hashlib.sha256(json.dumps([CACHE_VERSION, version("poke-env"), sorted(keys)])
                            .encode()).hexdigest()[:10]
    return out / "cache" / f"replay-{digest}"


def save_replay(path: Path, log_sha: str, replay: dict) -> None:
    meta = {k: v for k, v in replay.items() if k != "obs"}
    meta["log_sha256"] = log_sha
    arrays = {f"{key}__{name}": arr for key, obs in replay["obs"].items() for name, arr in obs.items()}
    tmp = path.with_name(path.stem + ".tmp.npz")
    np.savez_compressed(tmp, meta=np.array(json.dumps(meta)), **arrays)
    tmp.replace(path)


def load_replay(path: Path, log_sha: str, keys: Sequence[str]) -> Optional[dict]:
    if not path.exists():
        return None
    try:
        with np.load(path, allow_pickle=False) as npz:
            meta = json.loads(str(npz["meta"]))
            if meta.get("log_sha256") != log_sha:
                return None
            obs: Dict[str, Dict[str, np.ndarray]] = defaultdict(dict)
            for name in npz.files:
                if "__" in name:
                    key, input_name = name.split("__", 1)
                    obs[key][input_name] = npz[name]
    except (OSError, ValueError, KeyError):
        return None
    if any(key not in obs for key in keys):
        return None
    meta["obs"] = dict(obs)
    return meta


# --------------------------------------------------------------------------
# 3. Choices -> actions
# --------------------------------------------------------------------------
def align_choices(decision_lines: Sequence[int], error_lines: Sequence[int],
                  n_choices: int) -> Tuple[List[str], List[Optional[int]], int]:
    """Which of our inputlog choices answers each decision point. A decision point
    followed by an |error| before the next one was rejected (Showdown asks again),
    the choices answer the other ones in order, and those left once the choices run
    out were never committed (the battle ended first). Returns each decision's
    status ("committed", "rejected", "uncommitted"), its choice index, and the
    number of choices left over (more choices than decision points)."""
    statuses: List[str] = []
    indexes: List[Optional[int]] = []
    k = 0
    for i, line in enumerate(decision_lines):
        following = decision_lines[i + 1] if i + 1 < len(decision_lines) else math.inf
        if any(line < e < following for e in error_lines):
            statuses.append("rejected")
            indexes.append(None)
        elif k < n_choices:
            statuses.append("committed")
            indexes.append(k)
            k += 1
        else:
            statuses.append("uncommitted")
            indexes.append(None)
    return statuses, indexes, n_choices - k


def choice_to_action(choice: Choice, team: Sequence[str], request_team: Sequence[str],
                     moves: Sequence[str]) -> Tuple[Optional[int], Optional[str]]:
    """Our inputlog choice as a compact action, or (None, why it has none).

    team: battle.team's keys (switch action i = slot i); request_team: the idents of
    that request's side.pokemon ("switch N" is the N-th); moves: move_options' ids."""
    if choice.kind == "switch" and choice.position is not None:
        if not 1 <= choice.position <= len(request_team):
            return None, "switch position outside the request's team"
        ident = request_team[choice.position - 1]
        if ident not in team:
            return None, "switch target missing from battle.team"
        slot = list(team).index(ident)
        if slot >= E.N_SLOTS:
            return None, "switch target beyond team slot 6"
        return slot, None
    if choice.kind == "move" and choice.move:
        ids = [to_id(m) for m in moves]
        if choice.move not in ids:
            return None, "move not among poke-env's move actions"
        return (10 if choice.tera else 6) + ids.index(choice.move), None
    return None, f"no compact action for '{choice.raw}'"


def check_choice(logits: np.ndarray, mask: np.ndarray, action: Optional[int]) -> str:
    """Our choice against the argmax of the legal logits: "match", "tie" (another
    action is the argmax but our logit equals it), "mismatch", "illegal" (poke-env
    masks our choice out) or "unmapped" (no compact action)."""
    if action is None:
        return "unmapped"
    if not mask[action]:
        return "illegal"
    best = int(np.argmax(np.where(mask, logits, -np.inf)))
    if best == action:
        return "match"
    return "tie" if logits[best] - logits[action] <= TIE_TOLERANCE else "mismatch"


def describe_action(action: Optional[int], decision: dict) -> str:
    if action is None or action < 0:
        return "-"
    if action < 6:
        team = decision["team"]
        return f"switch {team[action].split(': ', 1)[-1]}" if action < len(team) else f"switch slot {action}"
    moves = decision["moves"]
    j = (action - 6) % 4
    name = moves[j] if j < len(moves) else f"move {j + 1}"
    return f"{name} + tera" if action >= 10 else name


# --------------------------------------------------------------------------
# 4. Models
# --------------------------------------------------------------------------
@dataclass
class Model:
    name: str        # folder under src/ElsaMina.Battles/Models
    encoder: object
    session: object
    key: str         # the encoder's identity: spec flags and set priors hash


def load_model(name: str) -> Model:
    import onnxruntime as ort

    from make_fixtures import encoder_from_spec  # imports torch: only when needed

    folder = MODELS_DIR / name
    spec_path = folder / "encoder_spec.json"
    spec = json.loads(spec_path.read_text())
    identity = [spec.get("model"), (spec.get("set_priors") or {}).get("sha256"), spec.get("encoder_version", 1)]
    key = hashlib.sha256(json.dumps(identity, sort_keys=True).encode()).hexdigest()[:10]
    session = ort.InferenceSession(str(folder / "minipoke.onnx"), providers=["CPUExecutionProvider"])
    return Model(name, encoder_from_spec(str(spec_path)), session, key)


def evaluate(model: Model, obs: Dict[str, np.ndarray], batch: int = 512) -> Tuple[np.ndarray, np.ndarray]:
    n = len(obs["mask"])
    logits, values = [np.zeros((0, E.ACT_DIM), np.float32)], [np.zeros(0, np.float32)]
    for start in range(0, n, batch):
        feed = {name: obs[name][start:start + batch] for name in model.encoder.input_names}
        out_logits, out_values = model.session.run(None, feed)
        logits.append(out_logits)
        values.append(out_values)
    return np.concatenate(logits), np.concatenate(values)


def policy(logits: np.ndarray, mask: np.ndarray) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Probabilities over the legal actions, argmax (-1 without one) and entropy."""
    legal = mask.any(axis=1)
    masked = np.where(mask, logits, -np.inf)
    masked[~legal] = 0.0
    shifted = np.exp(masked - masked.max(axis=1, keepdims=True))
    shifted[~mask] = 0.0
    probs = shifted / np.maximum(shifted.sum(axis=1, keepdims=True), 1e-30)
    entropy = -(probs * np.log(np.where(probs > 0, probs, 1.0))).sum(axis=1)
    argmax = np.where(legal, masked.argmax(axis=1), -1)
    return probs, argmax, entropy


def realized_returns(fainted: Sequence[Sequence[int]], final: Sequence[int], result: float) -> List[float]:
    """The return the value head learns at each decision (players.Trajectories): the
    faint difference until the next decision, the result on the last one, discounted."""
    n = len(fainted)
    rewards = []
    for t in range(n):
        after = fainted[t + 1] if t + 1 < n else final
        reward = FAINT_REWARD * ((after[1] - fainted[t][1]) - (after[0] - fainted[t][0]))
        rewards.append(reward + (result if t == n - 1 else 0.0))
    out, acc = [0.0] * n, 0.0
    for t in reversed(range(n)):
        acc = rewards[t] + GAMMA * acc
        out[t] = acc
    return out


# --------------------------------------------------------------------------
# ElsaMina's guard (MiniPokeActionGuard.cs): the same rules, on the same lines
# --------------------------------------------------------------------------
TRAPPED_ERROR = "[Unavailable choice] Can't switch: The active Pokémon is trapped"
# Their type, or what they hit, can change during a battle
GUARD_CHANGING_MOVES = {"terablast", "terastarstorm", "weatherball", "terrainpulse", "revelationdance",
                        "aurawheel", "endeavor", "dreameater", "synchronoise"}
GUARD_HAZARD_LAYERS = {"stealthrock": 1, "stickyweb": 1, "spikes": 3, "toxicspikes": 2}
GUARD_ENTRANCES = ("switch", "drag", "replace")
GUARD_OUTCOME_END = ("move", "switch", "drag", "replace", "turn", "cant", "upkeep")
GUARD_SLEEP_MOVES = {"sleeptalk", "snore"}
GUARD_REASONS = {"immune": "no effect on it before", "heal": "heals at full HP",
                 "asleep": "only works asleep", "hazard": "hazard already set", "trapped": "trapped"}


def guard_vetoes(lines: Sequence[str], ours: str, request: dict, moves: Sequence[str]) -> Dict[int, str]:
    """MiniPokeActionGuard.GetVetoes: the actions it vetoes (0-13) and why ("immune",
    "heal", "asleep", "hazard", "trapped"), from the lines received so far (the
    request included), our side, the request and the moves of actions 6-9
    (move_options)."""
    theirs = other(ours)
    active: Dict[str, Optional[str]] = {ours: None, theirs: None}
    last_entrance = trapped_error = -1
    immune: Dict[Tuple[str, str, str], int] = {}
    tera: Dict[Tuple[str, str], int] = {}
    hazards: Dict[str, Counter] = {ours: Counter(), theirs: Counter()}
    pending: Optional[Tuple[str, str]] = None
    for i, raw in enumerate(lines):
        line = raw.split("|")
        if len(line) < 2:
            continue
        kind = line[1]
        if kind in GUARD_OUTCOME_END:
            pending = None
        side, name = mon_key(line[2]) if len(line) > 2 else ("", "")
        if kind == "-swapsideconditions":
            hazards[ours], hazards[theirs] = hazards[theirs], hazards[ours]
        elif len(line) < 3:
            continue
        elif kind in GUARD_ENTRANCES:
            if side in active:
                active[side] = name
            last_entrance = i
        elif kind == "move" and side == ours and is_chosen_move(line):
            pending = (name, to_id(line[3]))
        elif kind == "-immune" and pending is not None and side == theirs:
            immune[(pending[0], pending[1], name)] = i
        elif kind == "-terastallize":
            tera[(side, name)] = i
        elif kind in ("-sidestart", "-sideend") and len(line) > 3 and side in hazards:
            condition = to_id(line[3].replace("move: ", ""))
            hazards[side][condition] = hazards[side][condition] + 1 if kind == "-sidestart" else 0
        elif kind == "error" and line[2].startswith(TRAPPED_ERROR):
            trapped_error = i

    vetoes: Dict[int, str] = {}
    me = next((p for p in (request.get("side") or {}).get("pokemon", []) if p.get("active")), None)
    hp = re.match(r"^(\d+)/(\d+)", (me or {}).get("condition", ""))
    full_hp = hp is not None and int(hp.group(2)) > 0 and hp.group(1) == hp.group(2)
    asleep = me is not None and ("slp" in me.get("condition", "").split(" ")
                                 or to_id(me.get("ability") or "") == "comatose")
    for j, move in enumerate(list(moves)[:E.N_MOVES]):
        line = immune.get((active[ours], move, active[theirs]))
        reason = None
        if (active[ours] is not None and active[theirs] is not None and move not in GUARD_CHANGING_MOVES
                and line is not None and line > tera.get((ours, active[ours]), -1)
                and line > tera.get((theirs, active[theirs]), -1)):
            reason = "immune"
        elif full_hp and (move == "rest" or (bool((MOVES.get(move) or {}).get("heal"))
                                             and (MOVES.get(move) or {}).get("target") == "self")):
            reason = "heal"
        elif not asleep and move in GUARD_SLEEP_MOVES:
            reason = "asleep"
        elif move in GUARD_HAZARD_LAYERS and hazards[theirs][move] >= GUARD_HAZARD_LAYERS[move]:
            reason = "hazard"
        if reason:
            vetoes[6 + j] = vetoes[10 + j] = reason
    force_switch = bool((request.get("forceSwitch") or [False])[0])
    if not force_switch and trapped_error > last_entrance:
        vetoes.update({slot: "trapped" for slot in range(E.N_SLOTS)})
    return vetoes


def is_chosen_move(line: Sequence[str]) -> bool:
    return len(line) > 3 and line[1] == "move" and not any(p.startswith("[from]") for p in line[4:])


def guarded(logits: np.ndarray, mask: np.ndarray, vetoes: Dict[int, str]) -> Tuple[int, np.ndarray]:
    """What ElsaMina plays with the guard: (action, the mask it took it from). The
    argmax, unless vetoed and another legal action is not: then the best of those."""
    best = int(np.argmax(np.where(mask, logits, -np.inf))) if mask.any() else -1
    if best not in vetoes:
        return best, mask
    allowed = mask.copy()
    allowed[list(vetoes)] = False
    if not allowed.any():
        return best, mask
    return int(np.argmax(np.where(allowed, logits, -np.inf))), allowed


# The search's candidates (search.py SearchConfig, MiniPokeSearchOptions.cs)
SEARCH_TOP_K = 3
SEARCH_MIN_PROB = 0.0


def search_candidates(logits: np.ndarray, allowed: np.ndarray) -> set:
    """The actions ElsaMina's search chooses among: the model's SEARCH_TOP_K most likely
    allowed ones (the guard's mask), the first, then those with at least SEARCH_MIN_PROB."""
    if allowed.sum() < 2:
        return set()
    weights = np.where(allowed, np.exp(logits - np.max(np.where(allowed, logits, -np.inf))), 0.0)
    probs = weights / weights.sum()
    ranked = [int(a) for a in np.argsort(-probs, kind="stable") if allowed[a]][:SEARCH_TOP_K]
    return {a for rank, a in enumerate(ranked) if rank == 0 or probs[a] >= SEARCH_MIN_PROB}


def request_at(lines: Sequence[str], line: int) -> dict:
    """The last |request| JSON up to that line."""
    for raw in reversed(lines[:line + 1]):
        if raw.startswith("|request|") and raw[len("|request|"):].strip():
            try:
                return json.loads(raw[len("|request|"):])
            except ValueError:
                return {}
    return {}


# --------------------------------------------------------------------------
# 5. Behavior, from the protocol (both sides)
# --------------------------------------------------------------------------
MOVES = GenData.from_gen(9).moves
HAZARD_LAYERS = {"stealthrock": 1, "stickyweb": 1, "spikes": 3, "toxicspikes": 2}
SETUP_WITHOUT_BOOSTS = {"bellydrum", "filletaway", "clangoroussoul"}


def is_setup(move: Optional[str]) -> bool:
    data = MOVES.get(move or "") or {}
    if move in SETUP_WITHOUT_BOOSTS:
        return True
    return (data.get("category") == "Status" and data.get("target") in ("self", "adjacentAllyOrSelf")
            and any(v > 0 for v in (data.get("boosts") or {}).values()))


def is_protect(move: Optional[str]) -> bool:
    return bool((MOVES.get(move or "") or {}).get("stallingMove"))


def inflicted_status(move: Optional[str]) -> Optional[str]:
    data = MOVES.get(move or "") or {}
    return data.get("status") if data.get("category") == "Status" else None


def mon_key(ident: str) -> Tuple[str, str]:
    """"p2a: Iron Valiant" or "p2: Iron Valiant" -> ("p2", "Iron Valiant")."""
    side, _, name = ident.partition(":")
    return side[:2], name.strip()


def hp_fraction(hp_status: str) -> Optional[float]:
    value = hp_status.split(" ")[0]
    if "/" in value:
        current, maximum = value.split("/")
        return int(current) / int(maximum) if int(maximum) else 0.0
    return 0.0 if value == "0" else None


def other(side: str) -> str:
    return "p2" if side == "p1" else "p1"


@dataclass
class SideBehavior:
    """One side's play in one battle. turn choices: one per turn that was played, a
    voluntary switch when that side's |switch| came before any |move|, |cant| or
    |faint| of the turn, a move otherwise (switches go before moves)."""
    turn_choices: int = 0
    voluntary_switches: int = 0
    switch_loops: List[dict] = field(default_factory=list)   # runs of 3+ voluntary switches
    chosen_moves: int = 0
    immune: int = 0
    immune_ability: int = 0
    immune_repeated: int = 0      # the same move into the same Pokémon, already immune to it before
    failed: int = 0
    failed_repeated: int = 0      # the same move of the same Pokémon, already failed before
    failed_moves: Counter = field(default_factory=Counter)
    repeats: List[Tuple[str, int, str]] = field(default_factory=list)  # (behavior, turn, move id)
    status_into_status: int = 0
    redundant_hazards: int = 0
    setup_low_hp: int = 0
    setup_low_hp_repeated: int = 0
    tera_turn: Optional[int] = None
    tera_left: Optional[Tuple[int, int]] = None   # (ours, theirs) alive when it terastallized
    examples: Dict[str, List[str]] = field(default_factory=lambda: defaultdict(list))


BEHAVIOR_COUNTS = [
    ("voluntary_switches", "Voluntary switches"),
    ("switch_loops", "Switch loops (3+ voluntary switches in a row)"),
    ("immune", "Moves into an immunity (It doesn't affect)"),
    ("immune_ability", "  of which into an ability ([from] ability)"),
    ("immune_repeated", "  of which again: same move, same target, immune before"),
    ("failed", "Failed moves (|-fail|, But it failed!)"),
    ("failed_repeated", "  of which again: same move of the same Pokémon, failed before"),
    ("status_into_status", "Status move into an already statused target"),
    ("redundant_hazards", "Hazard set while already at its max layers"),
    ("setup_low_hp", "Setup or Protect at <=33% HP"),
    ("setup_low_hp_repeated", "  repeated (its previous move was setup or Protect too)"),
]


def behavior_count(b: SideBehavior, name: str) -> int:
    value = getattr(b, name)
    return len(value) if isinstance(value, list) else value


def scan_behavior(lines: Sequence[str]) -> Dict[str, SideBehavior]:
    sides = {"p1": SideBehavior(), "p2": SideBehavior()}
    hp: Dict[Tuple[str, str], float] = {}
    status: Dict[Tuple[str, str], Optional[str]] = {}
    active: Dict[str, Optional[Tuple[str, str]]] = {"p1": None, "p2": None}
    hazards = {"p1": Counter(), "p2": Counter()}
    fainted = Counter()
    previous_move: Dict[str, Optional[str]] = {"p1": None, "p2": None}
    immune_before: Dict[str, set] = {"p1": set(), "p2": set()}   # (move, target name)
    failed_before: Dict[str, set] = {"p1": set(), "p2": set()}   # (user name, move)
    runs: Dict[str, List[Tuple[int, str]]] = {"p1": [], "p2": []}
    turn, acted, played = 0, False, False
    switched: Dict[str, Optional[str]] = {"p1": None, "p2": None}
    pending: Optional[dict] = None   # the last chosen move: its results follow it

    def close_run(side: str) -> None:
        if len(runs[side]) >= 3:
            sides[side].switch_loops.append({"turn": runs[side][0][0],
                                             "mons": [name for _, name in runs[side]]})
        runs[side] = []

    def end_turn() -> None:
        if turn < 1 or not played:
            return
        for side, b in sides.items():
            b.turn_choices += 1
            if switched[side] is not None:
                b.voluntary_switches += 1
                runs[side].append((turn, switched[side]))
            else:
                close_run(side)

    for line in lines:
        parts = line.split("|")
        if len(parts) < 2:
            continue
        kind = parts[1]
        if kind == "turn" and len(parts) > 2:
            end_turn()
            turn, acted, played = int(parts[2]), False, False
            switched = {"p1": None, "p2": None}
            pending = None
        elif kind in ("switch", "drag") and len(parts) > 4:
            key = mon_key(parts[2])
            side = key[0]
            if kind == "switch" and turn >= 1 and not acted:
                switched[side] = key[1]
            active[side] = key
            hp[key] = hp_fraction(parts[4]) or 0.0
            words = parts[4].split(" ")
            status[key] = words[1] if len(words) > 1 else None
            previous_move[side] = None
            played, pending = True, None
        elif kind == "replace" and len(parts) > 2:
            active[mon_key(parts[2])[0]] = mon_key(parts[2])
        elif kind == "move" and len(parts) > 3:
            key = mon_key(parts[2])
            side, foe = key[0], other(key[0])
            move = to_id(parts[3])
            acted = played = True
            if any(p.startswith("[from]") for p in parts[4:]):
                pending = None   # locked, called or reflected: not a choice
                continue
            b = sides[side]
            b.chosen_moves += 1
            where = f"turn {turn}: {key[1]} {parts[3]}"
            target = mon_key(parts[4]) if len(parts) > 4 and parts[4][:2] == foe else active[foe]
            if inflicted_status(move) and target is not None and status.get(target) not in (None, "fnt"):
                b.status_into_status += 1
                b.examples["status_into_status"].append(f"{where} into {target[1]} ({status[target]})")
            if move in HAZARD_LAYERS and hazards[foe][move] >= HAZARD_LAYERS[move]:
                b.redundant_hazards += 1
                b.examples["redundant_hazards"].append(where)
            if (is_setup(move) or is_protect(move)) and hp.get(key, 1.0) <= LOW_HP:
                b.setup_low_hp += 1
                note = f"{where} at {round(100 * hp.get(key, 1.0))}%"
                b.examples["setup_low_hp"].append(note)
                if is_setup(previous_move[side]) or is_protect(previous_move[side]):
                    b.setup_low_hp_repeated += 1
                    b.examples["setup_low_hp_repeated"].append(f"{note}, after {previous_move[side]}")
            previous_move[side] = move
            pending = {"side": side, "user": key[1], "move": move, "where": where, "immune": False,
                       "failed": False}
        elif kind == "cant":
            acted = played = True
            pending = None
        elif kind == "faint" and len(parts) > 2:
            key = mon_key(parts[2])
            status[key] = "fnt"
            fainted[key[0]] += 1
            acted = played = True
        elif kind == "-immune" and len(parts) > 2 and pending is not None:
            target = mon_key(parts[2])
            if target[0] != pending["side"] and not pending["immune"]:
                pending["immune"] = True
                b = sides[pending["side"]]
                b.immune += 1
                example = f"{pending['where']} into {target[1]}"
                b.examples["immune"].append(example)
                if any(p.startswith("[from] ability:") for p in parts[3:]):
                    b.immune_ability += 1
                    b.examples["immune_ability"].append(example)
                if (pending["move"], target[1]) in immune_before[pending["side"]]:
                    b.immune_repeated += 1
                    b.examples["immune_repeated"].append(example)
                    b.repeats.append(("immune_repeated", turn, pending["move"]))
                immune_before[pending["side"]].add((pending["move"], target[1]))
        elif kind == "-fail" and pending is not None and not pending["failed"]:
            pending["failed"] = True
            b = sides[pending["side"]]
            b.failed += 1
            b.failed_moves[pending["move"]] += 1
            b.examples["failed"].append(pending["where"])
            if (pending["user"], pending["move"]) in failed_before[pending["side"]]:
                b.failed_repeated += 1
                b.examples["failed_repeated"].append(pending["where"])
                b.repeats.append(("failed_repeated", turn, pending["move"]))
            failed_before[pending["side"]].add((pending["user"], pending["move"]))
        elif kind in ("-damage", "-heal", "-sethp") and len(parts) > 3:
            key = mon_key(parts[2])
            fraction = hp_fraction(parts[3])
            if fraction is not None:
                hp[key] = fraction
            if kind == "-heal" and "[from] move: Revival Blessing" in parts[4:]:
                status[key] = None
                fainted[key[0]] -= 1
        elif kind == "-status" and len(parts) > 3:
            status[mon_key(parts[2])] = parts[3]
        elif kind == "-curestatus" and len(parts) > 2:
            status[mon_key(parts[2])] = None
        elif kind == "-cureteam" and len(parts) > 2:
            for key in status:
                if key[0] == parts[2][:2] and status[key] != "fnt":
                    status[key] = None
        elif kind in ("-sidestart", "-sideend") and len(parts) > 3:
            condition = to_id(parts[3].replace("move: ", ""))
            if kind == "-sidestart":
                hazards[parts[2][:2]][condition] += 1
            else:
                hazards[parts[2][:2]][condition] = 0
        elif kind == "-swapsideconditions":
            hazards = {"p1": hazards["p2"], "p2": hazards["p1"]}
        elif kind == "-terastallize" and len(parts) > 2:
            side = parts[2][:2]
            if sides[side].tera_turn is None:
                sides[side].tera_turn = turn
                sides[side].tera_left = (6 - fainted[side], 6 - fainted[other(side)])
        elif kind in ("upkeep", ""):
            pending = None
    end_turn()
    for side in sides:
        close_run(side)
    return sides


# --------------------------------------------------------------------------
# 6. Statistics
# --------------------------------------------------------------------------
def wilson(k: float, n: int, z: float = 1.96) -> Tuple[float, float]:
    if n == 0:
        return float("nan"), float("nan")
    p = k / n
    centre = (p + z * z / (2 * n)) / (1 + z * z / n)
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / (1 + z * z / n)
    return centre - half, centre + half


def elo_expected(ours: float, theirs: float) -> float:
    return 1.0 / (1.0 + 10 ** ((theirs - ours) / 400.0))


def performance_rating(opponents: Sequence[float], score: float) -> float:
    """The rating whose Elo-expected score against these opponents is `score`."""
    if not opponents:
        return float("nan")
    low, high = 0.0, 4000.0
    for _ in range(60):
        mid = (low + high) / 2
        if sum(elo_expected(mid, r) for r in opponents) < score:
            low = mid
        else:
            high = mid
    return (low + high) / 2


def auc(scores: Sequence[float], labels: Sequence[bool]) -> float:
    """Probability that a won battle's decision scores above a lost one's (ties half)."""
    scores, labels = np.asarray(scores, float), np.asarray(labels, bool)
    pos, neg = labels.sum(), (~labels).sum()
    if pos == 0 or neg == 0:
        return float("nan")
    order = scores.argsort(kind="mergesort")
    ranks = np.empty(len(scores))
    sorted_scores = scores[order]
    i = 0
    while i < len(scores):
        j = i
        while j + 1 < len(scores) and sorted_scores[j + 1] == sorted_scores[i]:
            j += 1
        ranks[order[i:j + 1]] = (i + j) / 2 + 1
        i = j + 1
    return float((ranks[labels].sum() - pos * (pos + 1) / 2) / (pos * neg))


def bucket(value: float, edges: Sequence[float]) -> int:
    return int(np.searchsorted(edges, value, side="right"))


def rating_labels() -> List[str]:
    """bucket(rating, RATING_EDGES)'s labels; ratings start at the ladder floor, 1000."""
    edges = [1000] + RATING_EDGES
    return [f"{a}-{b - 1}" for a, b in zip(edges, edges[1:])] + [f"{RATING_EDGES[-1]}+"]


def value_labels() -> List[str]:
    """bucket(value, VALUE_EDGES)'s labels: [low, high) intervals."""
    labels = [f"below {VALUE_EDGES[0]:+.2f}"]
    labels += [f"{a:+.2f} to {b:+.2f}" for a, b in zip(VALUE_EDGES, VALUE_EDGES[1:])]
    return labels + [f"{VALUE_EDGES[-1]:+.2f} and up"]


def turn_labels() -> List[str]:
    edges = [1] + TURN_EDGES
    labels = [f"{a}-{b - 1}" if b - 1 > a else f"{a}" for a, b in zip(edges, edges[1:])]
    return labels + [f"{TURN_EDGES[-1]}+"]


def pct(x: float, digits: int = 1) -> str:
    return "n/a" if x != x else f"{100 * x:.{digits}f}%"


def fmt_time(ts: Optional[int]) -> str:
    return "" if ts is None else datetime.fromtimestamp(ts).astimezone().strftime("%Y-%m-%d %H:%M %Z")


def link(battle_id: str, text: Optional[str] = None) -> str:
    return f"[{text or battle_id.rsplit('-', 1)[-1]}]({replay_url(battle_id)})"


def table(header: Sequence[str], rows: Sequence[Sequence], align: Optional[str] = None) -> str:
    align = align or "l" + "r" * (len(header) - 1)
    marks = {"l": ":---", "r": "---:", "c": ":---:"}
    out = ["| " + " | ".join(header) + " |", "| " + " | ".join(marks[a] for a in align) + " |"]
    out += ["| " + " | ".join(str(c).replace("|", "\\|") for c in row) + " |" for row in rows]
    return "\n".join(out)


# --------------------------------------------------------------------------
# 7. Charts (matplotlib; the dataviz skill's reference palette, light surface)
# --------------------------------------------------------------------------
INK = {"surface": "#fcfcfb", "ink": "#0b0b0b", "ink2": "#52514e", "muted": "#898781",
       "grid": "#e1e0d9", "axis": "#c3c2b7"}
OURS, THEIRS = "#2a78d6", "#eb6834"   # categorical slots 1 and 2


def pyplot():
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    plt.rcParams.update({
        "figure.facecolor": INK["surface"], "axes.facecolor": INK["surface"],
        "savefig.facecolor": INK["surface"], "axes.edgecolor": INK["axis"],
        "axes.labelcolor": INK["ink2"], "text.color": INK["ink"],
        "xtick.color": INK["muted"], "ytick.color": INK["muted"],
        "xtick.labelcolor": INK["ink2"], "ytick.labelcolor": INK["ink2"],
        "axes.grid": True, "grid.color": INK["grid"], "grid.linewidth": 0.8,
        "axes.axisbelow": True, "axes.spines.top": False, "axes.spines.right": False,
        "axes.titlesize": 12, "axes.titleweight": "bold", "axes.titlelocation": "left",
        "font.size": 10, "lines.linewidth": 2, "lines.solid_capstyle": "round",
        "legend.frameon": False, "savefig.dpi": 150, "savefig.bbox": "tight",
    })
    return plt


def save(fig, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(path)
    fig.clf()
    import matplotlib.pyplot as plt

    plt.close(fig)


# --------------------------------------------------------------------------
# 8. Analysis
# --------------------------------------------------------------------------
@dataclass
class Battle:
    id: str
    lines: List[str]
    log: LogInfo
    replay: ReplayInfo
    decisions: List[dict]
    parse_errors: List[list]
    messages: List[str]
    final_fainted: List[int]
    behavior: Dict[str, SideBehavior] = field(default_factory=dict)
    leftover_choices: int = 0
    first_seen: Dict[str, int] = field(default_factory=dict)  # mechanic -> first line index
    obs: Dict[str, Dict[str, np.ndarray]] = field(default_factory=dict, repr=False)  # until evaluated
    played_by: str = PLAYED_BY
    attribution: str = ""            # how played_by was found: "choices", "nearest in time"
    model_matches: Dict[str, int] = field(default_factory=dict)  # model -> committed choices it explains
    guard: bool = False              # played with ElsaMina's guard (MiniPokeActionGuard)
    guard_attribution: str = ""      # how guard was found, like attribution
    guard_matches: Dict[str, int] = field(default_factory=dict)  # "without" / "with" -> choices explained
    search: bool = False             # played with ElsaMina's search (MiniPokeTurnSearch)
    search_attribution: str = ""     # how search was found, like attribution

    @property
    def us(self) -> str:
        return self.log.our_side

    @property
    def them(self) -> str:
        return self.log.opp_side

    @property
    def outcome(self) -> str:
        return self.log.outcome

    @property
    def result(self) -> float:
        return {"win": WIN_REWARD, "loss": -WIN_REWARD}.get(self.outcome, 0.0)

    @property
    def score(self) -> float:
        return {"win": 1.0, "loss": 0.0, "tie": 0.5}.get(self.outcome, float("nan"))

    @property
    def url(self) -> str:
        return replay_url(self.id)

    @property
    def ending(self) -> Tuple[str, Optional[str]]:
        return self.log.ending

    @property
    def ended_by(self) -> Optional[str]:
        side = self.ending[1]
        return None if side is None else ("us" if side == self.us else "opponent")

    @property
    def early_forfeit(self) -> bool:
        return self.ending == ("forfeit", self.them) and self.log.turns < EARLY_FORFEIT_TURN

    def rating(self, side: str) -> Optional[int]:
        before = self.log.ratings.get(side)
        if before is None:
            change = self.replay.rating_changes.get(to_id(self.log.names.get(side)))
            before = change[0] if change else None
        return before

    def rating_after(self, side: str) -> Optional[int]:
        change = self.replay.rating_changes.get(to_id(self.log.names.get(side)))
        return change[1] if change else None

    @property
    def rated(self) -> bool:
        return bool(self.replay.rating_changes) and self.rating(self.us) is not None \
            and self.rating(self.them) is not None


MECHANICS = {"illusion": ("|replace|", "Zoroark"), "transform": ("|-transform|",),
             "forme change": ("|detailschange|", "|-formechange|")}


def first_seen(lines: Sequence[str]) -> Dict[str, int]:
    out = {}
    for name, needles in MECHANICS.items():
        out[name] = next((i for i, line in enumerate(lines)
                          if any(n in line for n in needles) and not line.startswith("|request|")),
                         math.inf)
    # our own Zoroark only shows in our request
    zoroark = next((i for i, line in enumerate(lines) if line.startswith("|request|") and "Zoroark" in line),
                   math.inf)
    out["illusion"] = min(out["illusion"], zoroark)
    return out


def mismatch_cause(d: dict, battle: Battle) -> str:
    if d["after_error"]:
        return "re-choice after an |error|"
    if d["check"] == "unmapped":
        return f"choice has no compact action ({d['mapping_problem']})"
    if d["check"] == "illegal":
        return "choice is illegal in poke-env's mask"
    if d["gap"] is not None and d["gap"] < NEAR_TIE_LOGITS:
        return f"near tie (logit gap < {NEAR_TIE_LOGITS})"
    for mechanic in ("illusion", "transform", "forme change"):
        if battle.first_seen.get(mechanic, math.inf) <= d["line"]:
            return f"{mechanic} seen earlier in the battle"
    if d["force_switch"]:
        return "forced switch, unexplained"
    return "unexplained (tracker or encoder difference)"


def analyze(folder: Path, out: Path, refresh: bool, compare: bool, limit: Optional[int]) -> dict:
    t0 = time.time()
    stems, pairing_issues = pair_files(folder)
    if limit:
        stems = stems[:limit]
    broken: List[str] = []
    loaded = []
    for stem in stems:
        try:
            loaded.append((stem, *load_pair(folder, stem)))
        except BrokenPair as exc:
            broken.append(f"`{stem}`: {exc}")
    print(f"{len(loaded)} battles paired ({len(broken)} broken, {len(pairing_issues)} unpaired files)")

    models = [load_model(PLAYED_BY)] + ([load_model(COMPARE_WITH)] if compare else [])
    encoders = {}
    for model in models:
        encoders.setdefault(model.key, model.encoder)
    cache = cache_folder(out, list(encoders))
    cache.mkdir(parents=True, exist_ok=True)

    battles: List[Battle] = []
    cached = 0
    for stem, lines, log, replay in loaded:
        log_sha = hashlib.sha256("\n".join(lines).encode()).hexdigest()
        path = cache / f"{stem}.npz"
        replayed = None if refresh else load_replay(path, log_sha, list(encoders))
        if replayed is None:
            replayed = replay_battle(stem, lines, log.our_name, encoders)
            save_replay(path, log_sha, replayed)
        else:
            cached += 1
        battles.append(Battle(stem, lines, log, replay, replayed["decisions"], replayed["parse_errors"],
                              replayed["messages"], replayed["final_fainted"], obs=replayed["obs"]))
    print(f"replayed {len(battles) - cached} battles, {cached} from the cache ({time.time() - t0:.0f}s)")

    # The models, on every decision point at once
    outputs = {}
    for model in models:
        obs = {name: np.concatenate([b.obs[model.key][name] for b in battles])
               for name in model.encoder.input_names}
        logits, values = evaluate(model, obs)
        probs, argmax, entropy = policy(logits, obs["mask"].astype(bool))
        outputs[model.name] = {"logits": logits, "value": values, "probs": probs,
                               "argmax": argmax, "entropy": entropy, "mask": obs["mask"].astype(bool)}
    print(f"ran {', '.join(m.name for m in models)} on "
          f"{len(outputs[PLAYED_BY]['value'])} decision points ({time.time() - t0:.0f}s)")

    index = 0
    for battle in battles:
        battle.obs = {}
        battle.first_seen = first_seen(battle.lines)
        battle.behavior = scan_behavior(battle.lines)
        ours = battle.replay.choices_of(battle.us)
        statuses, choice_index, battle.leftover_choices = align_choices(
            [d["line"] for d in battle.decisions], [i for i, _ in battle.log.errors], len(ours))
        returns = realized_returns([d["fainted"] for d in battle.decisions], battle.final_fainted,
                                   battle.result)
        for k, d in enumerate(battle.decisions):
            d["index"] = index
            d["status"] = statuses[k]
            d["after_error"] = k > 0 and statuses[k - 1] == "rejected"
            d["return"] = returns[k]
            choice = ours[choice_index[k]] if choice_index[k] is not None else None
            d["choice"] = choice.raw.split(" ", 1)[1] if choice else None
            d["action"], d["mapping_problem"] = (choice_to_action(choice, d["team"], d["request_team"], d["moves"])
                                                 if choice else (None, None))
            d["mask"] = outputs[PLAYED_BY]["mask"][index]
            d["vetoes"] = guard_vetoes(battle.lines[:d["line"] + 1], battle.us,
                                       request_at(battle.lines, d["line"]), d["moves"])
            d["models"] = {}
            for name, o in outputs.items():
                own = {key: o[key][index] for key in ("logits", "probs", "argmax", "value", "entropy")}
                own["guarded"], own["guarded_mask"] = guarded(own["logits"], d["mask"], d["vetoes"])
                own["candidates"] = set() if d["force_switch"] else search_candidates(
                    own["logits"], own["guarded_mask"])
                d["models"][name] = own
            index += 1
    attribute_models(battles, list(outputs))
    for battle in battles:
        for d in battle.decisions:
            own = d["models"][battle.played_by]
            d["probs"], d["value"], d["entropy"] = own["probs"], float(own["value"]), float(own["entropy"])
            d["argmax"] = int(own["guarded"]) if battle.guard else int(own["argmax"])
            # what the guard changes, whether this battle was played with it or not
            d["guard_override"] = (d["vetoes"][int(own["argmax"])]
                                   if int(own["guarded"]) != int(own["argmax"]) else None)
            mask = own["guarded_mask"] if battle.guard else d["mask"]
            d["check"] = check_choice(own["logits"], mask, d["action"]) if d["choice"] else None
            if battle.search and d["check"] in ("mismatch", "illegal") and d["action"] in own["candidates"]:
                d["check"] = "searched"
            d["gap"] = (float(own["logits"][d["argmax"]] - own["logits"][d["action"]])
                        if d["check"] in ("mismatch", "tie") else None)
            others = [name for name in d["models"] if name != battle.played_by]
            if others:
                other = d["models"][others[0]]
                d["other_model"], d["other_argmax"] = others[0], int(other["argmax"])
                d["other_probs"], d["other_value"] = other["probs"], float(other["value"])
            if d["check"] in ("mismatch", "illegal", "unmapped"):
                d["cause"] = mismatch_cause(d, battle)
    return {"battles": battles, "pairing_issues": pairing_issues, "broken": broken,
            "models": [m.name for m in models], "folder": folder, "seconds": time.time() - t0}


def explains(d: dict, name: str, guard: bool) -> bool:
    """`name`'s argmax (with the guard or without) is the committed choice."""
    own = d["models"][name]
    mask = own["guarded_mask"] if guard else d["mask"]
    return check_choice(own["logits"], mask, d["action"]) in ("match", "tie")


def nearest_in_time(b: Battle, decided: List[Battle]) -> Optional[Battle]:
    return min(decided, key=lambda o: abs((o.log.start_time or 0) - (b.log.start_time or 0)), default=None)


def attribute_models(battles: List[Battle], names: Sequence[str]) -> None:
    """Which model played each battle: the one whose argmax (with ElsaMina's guard or
    without) explains strictly more of its committed choices than any other. Then
    whether it played with the guard: the variant that explains strictly more. A
    battle no choice tells apart (none committed, or both explain them all) takes the
    model (or the guard) of the nearest such battle in time: ElsaMina's strategy is
    switched by hand, between battles, and the guard came with a new build."""
    decided, guard_decided = [], []
    for b in battles:
        committed = [d for d in b.decisions if d["status"] == "committed" and d["choice"]]
        b.model_matches = {name: sum(explains(d, name, False) or explains(d, name, True) for d in committed)
                           for name in names}
        best = max(b.model_matches.values(), default=0)
        winners = [name for name in names if b.model_matches[name] == best]
        if best > 0 and len(winners) == 1:
            b.played_by, b.attribution = winners[0], "choices"
            decided.append(b)
    for b in battles:
        if not b.attribution:
            nearest = nearest_in_time(b, decided)
            b.played_by = nearest.played_by if nearest is not None else names[0]
            b.attribution = "nearest in time" if nearest is not None else "default"
        committed = [d for d in b.decisions if d["status"] == "committed" and d["choice"]]
        b.guard_matches = {"without": sum(explains(d, b.played_by, False) for d in committed),
                           "with": sum(explains(d, b.played_by, True) for d in committed)}
        if b.guard_matches["with"] != b.guard_matches["without"]:
            b.guard = b.guard_matches["with"] > b.guard_matches["without"]
            b.guard_attribution = "choices"
            guard_decided.append(b)
    for b in battles:
        if not b.guard_attribution:
            nearest = nearest_in_time(b, guard_decided)
            b.guard = nearest.guard if nearest is not None else False
            b.guard_attribution = "nearest in time" if nearest is not None else "default"
    attribute_search(battles)


def attribute_search(battles: List[Battle]) -> None:
    """Whether a battle was played with the search: some choice is not the (guarded)
    argmax, and every choice is the argmax or one of the search's candidates. When
    every choice is the argmax (the search agreed every time, or never ran), the
    nearest battle in time decides, as for the guard."""
    decided = []
    for b in battles:
        committed = [d for d in b.decisions if d["status"] == "committed" and d["choice"]]
        plain = sum(explains(d, b.played_by, b.guard) for d in committed)
        possible = sum(explains(d, b.played_by, True)
                       or d["action"] in d["models"][b.played_by]["candidates"] for d in committed)
        if plain < len(committed):
            b.search = possible == len(committed)
            b.search_attribution = "choices"
            decided.append(b)
    for b in battles:
        if not b.search_attribution:
            nearest = nearest_in_time(b, decided)
            b.search = nearest.search if nearest is not None else False
            b.search_attribution = "nearest in time" if nearest is not None else "default"


# --------------------------------------------------------------------------
# 9. Report sections
# --------------------------------------------------------------------------
def strategy(model: str) -> str:
    """"MiniPokeV2_1M" -> "minipokev2-1m", "MiniPokeV2_1_1M" -> "minipokev2.1-1m": ElsaMina's name for it
    (BattleDecisionStrategyExtensions.GetName)."""
    version, _, size = model.lower().rpartition("_")
    return f"{version.replace('_', '.')}-{size}" if version else size


def win_summary(name: str, group: List[Battle]) -> list:
    wins = sum(b.outcome == "win" for b in group)
    losses = sum(b.outcome == "loss" for b in group)
    ties = sum(b.outcome == "tie" for b in group)
    n = wins + losses + ties
    lo, hi = wilson(wins + 0.5 * ties, n)
    return [name, n, f"{wins}-{losses}-{ties}", pct((wins + 0.5 * ties) / n if n else float("nan")),
            f"{pct(lo)} to {pct(hi)}"]


def results_section(battles: List[Battle], charts: Path) -> Tuple[str, dict]:
    """By model over the whole folder, then minipokev2-1m's battles in detail."""
    models = [PLAYED_BY] + sorted({b.played_by for b in battles} - {PLAYED_BY})
    model_rows = []
    for model in models:
        group = [b for b in battles if b.played_by == model]
        starts = [b.log.start_time for b in group if b.log.start_time]
        model_rows.append(win_summary(f"`{strategy(model)}`", group)
                          + [f"{fmt_time(min(starts))} to {fmt_time(max(starts))}" if starts else ""])
    model_rows.append(win_summary("All battles in the folder", battles) + [""])

    decided = [b for b in battles if b.played_by == PLAYED_BY and b.outcome in ("win", "loss", "tie")]
    rows = [win_summary("All its battles", decided),
            win_summary(f"Without opponent forfeits before turn {EARLY_FORFEIT_TURN}",
                        [b for b in decided if not b.early_forfeit]),
            win_summary("Played to the last Pokémon (no forfeit, no inactivity)",
                        [b for b in decided if b.ending[0] == "all fainted"])]
    text = ["## Results", "",
            table(["Played by", "n", "W-L-T", "Win rate", "95% Wilson interval", "Started"], model_rows,
                  align="lrrrrl"), "",
            f"Which model played each battle comes from its choices (see Parity). The rest of this "
            f"section, the value head and the behavior sections are about `{strategy(PLAYED_BY)}`'s "
            f"{len(decided)} battles only.", "",
            f"### `{strategy(PLAYED_BY)}`", "",
            table(["Battles", "n", "W-L-T", "Win rate", "95% Wilson interval"], rows), ""]

    rated = [b for b in decided if b.rated]
    stats = {"models": model_rows, "all": rows[0], "no_early": rows[1], "to_the_end": rows[2],
             "wins": sum(b.outcome == "win" for b in decided), "n": len(decided), "n_rated": len(rated)}
    if not rated:
        return "\n".join(text + ["No rated battle: no rating analysis.", ""]), stats
    groups: Dict[int, List[Battle]] = defaultdict(list)
    for b in rated:
        groups[bucket(b.rating(b.them), RATING_EDGES)].append(b)
    bucket_rows, chart_rows = [], []
    for i, label in enumerate(rating_labels()):
        group = groups.get(i, [])
        if not group:
            continue
        actual = sum(b.score for b in group)
        expected = sum(elo_expected(b.rating(b.us), b.rating(b.them)) for b in group)
        lo, hi = wilson(actual, len(group))
        bucket_rows.append([label, len(group), f"{sum(b.outcome == 'win' for b in group)}-"
                            f"{sum(b.outcome == 'loss' for b in group)}", pct(actual / len(group)),
                            f"{pct(lo)} to {pct(hi)}", pct(expected / len(group)),
                            f"{actual - expected:+.1f}"])
        chart_rows.append((label, actual / len(group), lo, hi, expected / len(group), len(group)))
    actual = sum(b.score for b in rated)
    expected = sum(elo_expected(b.rating(b.us), b.rating(b.them)) for b in rated)
    perf = performance_rating([b.rating(b.them) for b in rated], actual)
    text += [f"#### By opponent rating (before the battle, {len(rated)} rated battles)", "",
             table(["Opponent rating", "n", "W-L", "Score", "95% interval", "Elo-expected score",
                    "Actual - expected (games)"], bucket_rows), "",
             f"Over the rated battles: actual score {actual:.1f} vs Elo-expected {expected:.1f} "
             f"({actual - expected:+.1f} games, {pct(actual / len(rated))} vs {pct(expected / len(rated))}). "
             f"Performance rating (the Elo rating whose expected score against these opponents "
             f"is the actual one): **{perf:.0f}**. Elo-expected uses both ratings from the "
             f"`|player|` lines before each battle. The ladder floor is 1000, so a 1000-rated "
             f"opponent can be a new account of any strength.", "",
             "![Score by opponent rating](charts/score_by_rating.png)", ""]

    plt = pyplot()
    fig, ax = plt.subplots(figsize=(8, 3.6))
    x = np.arange(len(chart_rows))
    ax.bar(x, [r[1] for r in chart_rows], width=0.35, color=OURS, label="Actual score", zorder=2)
    ax.errorbar(x, [r[1] for r in chart_rows],
                yerr=[[r[1] - r[2] for r in chart_rows], [r[3] - r[1] for r in chart_rows]],
                fmt="none", ecolor=INK["ink2"], elinewidth=1, capsize=3, zorder=3)
    ax.scatter(x, [r[4] for r in chart_rows], s=50, color=THEIRS, edgecolor=INK["surface"],
               linewidth=2, label="Elo-expected score", zorder=4)
    ax.set_xticks(x, [f"{r[0]}\n(n={r[5]})" for r in chart_rows])
    ax.set_ylim(0, 1)
    ax.yaxis.set_major_formatter(lambda v, _: f"{v:.0%}")
    ax.set_xlabel("Opponent rating before the battle")
    ax.set_title(f"{strategy(PLAYED_BY)}: score by opponent rating, 95% Wilson intervals")
    ax.legend(loc="upper right", ncols=2)
    ax.grid(axis="x", visible=False)
    save(fig, charts / "score_by_rating.png")

    # The account's rating over every rated battle, other models' battles shaded
    timeline = sorted(((b.log.end_time or 0, b.rating_after(b.us), b) for b in battles
                       if b.rated and b.rating_after(b.us) is not None), key=lambda t: t[0])
    ours = [t for t in timeline if t[2].played_by == PLAYED_BY]
    first = min(rated, key=lambda b: b.log.start_time or 0)
    start_rating = first.rating(first.us)
    peak = max(ours, key=lambda t: t[1])
    low = min(ours, key=lambda t: t[1])
    final = ours[-1]
    import matplotlib.dates as mdates

    fig, ax = plt.subplots(figsize=(8, 3.6))
    xs, ys = [], []
    for i, (when, rating, _) in enumerate(timeline):
        if i and when - timeline[i - 1][0] > 3600:   # the bot was off for an hour: no line across
            xs.append(datetime.fromtimestamp(timeline[i - 1][0] + 1))
            ys.append(float("nan"))
        xs.append(datetime.fromtimestamp(when))
        ys.append(rating)
    ax.plot(xs, ys, color=OURS, linewidth=2)
    runs: List[List[Battle]] = []   # consecutive battles of another model
    previous: Optional[Battle] = None
    for _, _, b in timeline:
        if b.played_by != PLAYED_BY:
            if previous is not None and previous.played_by == b.played_by:
                runs[-1].append(b)
            else:
                runs.append([b])
        previous = b
    for run in runs:
        start = datetime.fromtimestamp(min(b.log.start_time for b in run))
        end = datetime.fromtimestamp(max(b.log.end_time for b in run))
        ax.axvspan(start, end, color=INK["grid"], zorder=0)
    for when, name in [(peak, "peak"), (final, "last")]:
        point = (datetime.fromtimestamp(when[0]), when[1])
        ax.scatter([point[0]], [point[1]], s=50, color=OURS, edgecolor=INK["surface"], linewidth=2, zorder=3)
        ax.annotate(f"{name} {when[1]}", point, textcoords="offset points", xytext=(0, 8), ha="center",
                    color=INK["ink2"], fontsize=9)
    ax.xaxis.set_major_locator(mdates.HourLocator(byhour=[0, 6, 12, 18]))
    ax.xaxis.set_major_formatter(mdates.DateFormatter("%a %H:%M"))
    ax.set_ylabel("Rating after the battle")
    shaded = sorted({strategy(run[0].played_by) for run in runs})
    ax.set_title("Elsa-Mina's gen9randombattle rating over time"
                 + (f" (shaded: {', '.join(shaded)})" if shaded else ""))
    save(fig, charts / "rating.png")
    account_final = timeline[-1]
    text += ["#### Rating over time", "",
             f"`{strategy(PLAYED_BY)}` took the account from {start_rating} (before its first battle, "
             f"{fmt_time(first.log.start_time)}) to {final[1]} (after its last, {fmt_time(final[0])}): "
             f"peak {peak[1]} ({fmt_time(peak[0])}), low {low[1]} ({fmt_time(low[0])})."
             + (f" The account ended at {account_final[1]} after the other models' battles (shaded)."
                if account_final[2].played_by != PLAYED_BY else ""), "",
             "![Rating over time](charts/rating.png)", ""]
    stats.update({"perf": perf, "actual": actual, "expected": expected, "start_rating": start_rating,
                  "peak": peak, "final": final, "account_final": account_final, "buckets": chart_rows})
    return "\n".join(text), stats


def endings_section(battles: List[Battle]) -> Tuple[str, dict]:
    counts = Counter((b.ending[0], b.ended_by) for b in battles)
    early = Counter(b.ending[0] for b in battles if b.ending[1] == b.them
                    and b.ending[0] in ("forfeit", "inactivity") and b.log.turns < EARLY_FORFEIT_TURN)
    rows = []
    for (kind, by), n in sorted(counts.items(), key=lambda kv: -kv[1]):
        meaning = {"forfeit": "forfeited", "inactivity": "lost due to inactivity",
                   "all fainted": "ran out of Pokémon", "tie": "tie", "no result": "no |win| line",
                   "other": "lost (other)"}[kind]
        who = {"us": "Elsa-Mina", "opponent": "the opponent", None: "-"}[by]
        early_n = early.get(kind, 0) if by == "opponent" else 0
        rows.append([f"{who} {meaning}" if by else meaning, n, early_n if kind in ("forfeit", "inactivity") else ""])
    text = ["## Endings", "",
            table(["Ending", "Battles", f"of which before turn {EARLY_FORFEIT_TURN}"], rows), ""]
    ours = [b for b in battles if b.ended_by == "us" and b.ending[0] in ("forfeit", "inactivity", "other", "no result")]
    if ours:
        text += ["**Bot bugs: battles the bot forfeited, timed out or ended without a result:**", ""]
        text += [f"- {link(b.id)}: {b.ending[0]} at turn {b.log.turns}" for b in ours]
    else:
        text += ["The bot never forfeited and never lost on time: every forfeit and every "
                 "inactivity loss is the opponent's.", ""]
    forcelose = Counter()
    for b in battles:
        for side in b.replay.forcelose:
            forcelose["us" if side == b.us else "opponent"] += 1
    text += [f"The inputlogs agree: `>forcelose` names the opponent {forcelose['opponent']} times "
             f"and the bot {forcelose['us']} times.", ""]

    error_rows = []
    for b in battles:
        for k, d in enumerate(b.decisions):
            if d["status"] != "rejected":
                continue
            error = next((text_ for i, text_ in b.log.errors if i > d["line"]), "")
            nxt = b.decisions[k + 1] if k + 1 < len(b.decisions) else None
            active = (json.loads(b.lines[d["line"]][len("|request|"):]).get("active") or [{}])[0]
            flags = [f for f in ("trapped", "maybeTrapped") if active.get(f)]
            error_rows.append([
                link(b.id), f"`{strategy(b.played_by)}`", d["turn"], error.replace("[Unavailable choice] ", ""),
                d["state"], ", ".join(flags) or "-", describe_action(d["argmax"], d),
                "trapped" if nxt and nxt["trapped"] else "-",
                describe_action(nxt["argmax"], nxt) if nxt else "-",
                nxt["choice"] if nxt and nxt["choice"] else "(none)"])
    n_errors = sum(len(b.log.errors) for b in battles)
    text += [f"### |error| lines ({n_errors})", ""]
    if error_rows:
        text += ["Each |error| rejects the bot's choice and Showdown sends the request again. "
                 "Our argmax at the rejected request is what the bot sent (the error confirms it "
                 "was a switch); the re-sent request is a new decision point. A request with "
                 "`maybeTrapped` (the opponent may have Shadow Tag, Arena Trap or Magnet Pull, not "
                 "revealed yet) leaves the switches in poke-env's mask, which only `trapped` clears, so "
                 "the model can pick one, as poke-env players do in training.", "",
                 table(["Battle", "Model", "Turn", "Error", "State (ours vs theirs)", "Request said",
                        "Bot sent (argmax)", "Re-sent request", "Argmax then", "Committed (inputlog)"],
                       error_rows, align="llrlllllll"), ""]
    stats = {"counts": counts, "ours": ours, "errors": error_rows, "early": early}
    return "\n".join(text), stats


def parity_section(battles: List[Battle], models: Sequence[str]) -> Tuple[str, dict]:
    decisions = [d for b in battles for d in b.decisions]
    status = Counter(d["status"] for d in decisions)
    checks = Counter(d["check"] for d in decisions if d["status"] == "committed")
    committed = status["committed"]
    matched = checks["match"] + checks["tie"] + checks["searched"]
    rate = matched / committed if committed else float("nan")
    lo, hi = wilson(matched, committed)

    played = [PLAYED_BY] + sorted({b.played_by for b in battles} - {PLAYED_BY})
    who_rows = []
    for model in played:
        group = [b for b in battles if b.played_by == model]
        n_choices = sum(sum(d["status"] == "committed" and d["choice"] is not None for d in b.decisions)
                        for b in group)
        by_time = [b for b in group if b.attribution != "choices"]
        who_rows.append([f"`{strategy(model)}`", len(group), len(group) - len(by_time), len(by_time), n_choices]
                        + [pct(sum(b.model_matches.get(m, 0) for b in group) / n_choices) if n_choices else "n/a"
                           for m in models])
    others = [b for b in battles if b.played_by != PLAYED_BY]
    text = ["## Parity (C# tracker and encoder vs poke-env)", "",
            "ElsaMina plays the argmax of the legal logits. Replaying each log into poke-env 0.16.1, "
            "encoding every decision point with encoder_v2.py and running the ONNX model, the argmax "
            "should be the choice the bot committed. Each model below was run on every decision point, "
            "and a battle is attributed to the model whose argmax explains strictly more of its committed "
            "choices; a battle no choice tells apart (no choice committed, or every model explains all "
            "of them) takes the model of the nearest attributed battle in time.", "",
            table(["Played by", "Battles", "attributed by choices", "by time", "Committed choices"]
                  + [f"{m} explains" for m in models], who_rows), ""]
    if others:
        starts = sorted(b.log.start_time for b in others if b.log.start_time)
        text += [f"**{len(others)} battles were not played by `{strategy(PLAYED_BY)}`** "
                 f"({', '.join(sorted({f'`{strategy(b.played_by)}`' for b in others}))}, started "
                 f"{fmt_time(starts[0])} to {fmt_time(starts[-1])}): the model that played them "
                 f"explains all their choices, `{strategy(PLAYED_BY)}` does not. Each battle is checked "
                 f"against the model that played it below.", ""]
    text += [table(["Decision points", "Count"], [
                ["poke-env choose_move calls", len(decisions)],
                ["answered by a committed inputlog choice", committed],
                ["rejected by an |error| (asked again)", status["rejected"]],
                ["never committed (the battle ended first)", status["uncommitted"]],
            ]), "",
            table(["Committed choice vs the argmax of the model that played", "Count", "Share"], [
                ["argmax (match)", checks["match"], pct(checks["match"] / committed)],
                ["tie with the argmax (counts as a match)", checks["tie"], pct(checks["tie"] / committed)],
                ["the search's choice among the model's candidates (counts as a match)", checks["searched"],
                 pct(checks["searched"] / committed)],
                ["another action (mismatch)", checks["mismatch"], pct(checks["mismatch"] / committed)],
                ["illegal in poke-env's mask", checks["illegal"], pct(checks["illegal"] / committed)],
                ["no compact action", checks["unmapped"], pct(checks["unmapped"] / committed)],
            ]), "",
            f"**Match rate: {pct(rate, 2)}** ({matched} of {committed}; 95% interval "
            f"{pct(lo, 2)} to {pct(hi, 2)}). By model: "
            + ", ".join(f"`{strategy(m)}` {per_model_rate(battles, m)}" for m in played) + ".", ""]
    bad = [(b, d) for b in battles for d in b.decisions if d.get("cause")]
    by_cause: Dict[str, List[Tuple[Battle, dict]]] = defaultdict(list)
    for b, d in bad:
        by_cause[d["cause"]].append((b, d))
    if bad:
        text += [f"### Mismatches ({len(bad)}, in {len({b.id for b, _ in bad})} battles), by likely cause", ""]
        text += [table(["Likely cause", "Decisions"], [[c, len(v)] for c, v in
                                                        sorted(by_cause.items(), key=lambda kv: -len(kv[1]))]), ""]
        for cause, items in sorted(by_cause.items(), key=lambda kv: -len(kv[1])):
            rows = []
            for b, d in items:
                top = np.argsort(-d["probs"])[:3]
                rows.append([link(b.id), d["turn"], "forced" if d["force_switch"] else "turn", d["state"],
                             d["choice"], describe_action(d["action"], d) if d["action"] is not None else "-",
                             describe_action(d["argmax"], d),
                             ", ".join(f"{describe_action(int(a), d)} {d['probs'][a]:.2f}" for a in top
                                       if d["mask"][a])])
            text += [f"#### {cause} ({len(items)})", "",
                     table(["Battle", "Turn", "Request", "State (poke-env)", "Committed", "As action",
                            "Argmax", "Top probabilities"], rows, align="lrllllll"), ""]
    else:
        text += ["No mismatch: poke-env's tracker and encoder_v2.py reproduce every committed choice, so "
                 "ElsaMina's C# tracker and encoder agree with them on every state the bot played.", ""]
    guard_text, guard_stats = guard_subsection(battles)
    text += guard_text
    ambiguous = [b for b in battles if b.attribution != "choices"]
    if ambiguous:
        text += ["<details><summary>Battles attributed by time</summary>", "",
                 table(["Battle", "Started", "Committed choices", "Played by"],
                       [[link(b.id), fmt_time(b.log.start_time),
                         sum(d["status"] == "committed" for d in b.decisions), f"`{strategy(b.played_by)}`"]
                        for b in ambiguous], align="llrl"), "", "</details>", ""]
    stats = {"rate": rate, "matched": matched, "committed": committed, "checks": checks,
             "status": status, "by_cause": {c: len(v) for c, v in by_cause.items()},
             "bad_battles": len({b.id for b, _ in bad}), "bad": bad, "others": others,
             "who": who_rows, "ambiguous": len(ambiguous), "guard": guard_stats}
    return "\n".join(text), stats


def guard_subsection(battles: List[Battle]) -> Tuple[List[str], dict]:
    """ElsaMina's guard: which battles were played with it, and the decisions it
    changes (or would have changed) for the model that played."""
    with_guard = [b for b in battles if b.guard]
    by_time = sum(b.guard_attribution != "choices" for b in with_guard)
    overrides = [(b, d) for b in battles for d in b.decisions if d.get("guard_override")]
    counts = Counter((d["guard_override"], b.guard) for b, d in overrides)
    without = [(b, d) for b, d in overrides if not b.guard]
    n_without = sum(len(b.decisions) for b in battles if not b.guard)
    rows = [[GUARD_REASONS[r], counts[(r, False)], counts[(r, True)]] for r in GUARD_REASONS]
    text = ["### ElsaMina's guard", "",
            "MiniPokeActionGuard.cs vetoes a move our active Pokémon already used on the opposing active with "
            "no effect (neither terastallized since), a move that only heals at full HP, Sleep Talk and Snore "
            "while awake, a hazard already at "
            "its max layers, and switches after Showdown said our active is trapped (until either active "
            "changes); the bot then plays the best action left. guard_vetoes() applies the same rules to each "
            "decision point, and a battle counts as played with the guard when that explains strictly more "
            f"of its choices. **{len(with_guard)} of {len(battles)} battles were played with it** "
            f"({len(with_guard) - by_time} by their choices, {by_time} by time).", "",
            table(["Vetoed (the model's argmax)", "Decisions changed, battles without the guard",
                   "with the guard"], rows), ""]
    if without:
        text += [f"Without the guard, {len(without)} of {n_without} decisions "
                 f"({pct(len(without) / n_without, 2) if n_without else 'n/a'}) in "
                 f"{len({b.id for b, _ in without})} battles would have changed:", "",
                 table(["Battle", "Turn", "State (poke-env)", "Played", "With the guard", "Why"],
                       [[link(b.id), d["turn"], d["state"], describe_action(int(d["models"][b.played_by]["argmax"]), d),
                         describe_action(int(d["models"][b.played_by]["guarded"]), d),
                         GUARD_REASONS[d["guard_override"]]] for b, d in without[:15]], align="lrllll"), ""]
    stats = {"battles": len(with_guard), "by_time": by_time, "counts": counts, "without": len(without),
             "decisions_without": n_without, "battles_changed": len({b.id for b, _ in without})}
    return text, stats


def per_model_rate(battles: List[Battle], model: str) -> str:
    ds = [d for b in battles if b.played_by == model for d in b.decisions if d["status"] == "committed"]
    matched = sum(d["check"] in ("match", "tie", "searched") for d in ds)
    return f"{pct(matched / len(ds), 2)} ({matched} of {len(ds)})" if ds else "n/a"


def value_section(battles: List[Battle], charts: Path) -> Tuple[str, dict]:
    rows = [(d, b) for b in battles if b.outcome in ("win", "loss") for d in b.decisions]
    values = np.array([d["value"] for d, _ in rows])
    returns = np.array([d["return"] for d, _ in rows])
    won = np.array([b.outcome == "win" for _, b in rows])
    turns = np.array([d["turn"] for d, _ in rows])
    labels = value_labels()
    which = np.array([bucket(v, VALUE_EDGES) for v in values])
    calibration = []
    for i, label in enumerate(labels):
        sel = which == i
        if sel.sum() == 0:
            continue
        calibration.append([label, int(sel.sum()), f"{values[sel].mean():+.3f}", f"{returns[sel].mean():+.3f}",
                            f"{(2 * won[sel].mean() - 1):+.3f}", pct(won[sel].mean())])
    turn_bucket = np.array([bucket(t, TURN_EDGES) for t in turns])
    by_turn = []
    for i, label in enumerate(turn_labels()):
        sel = turn_bucket == i
        if sel.sum() < 2:
            continue
        by_turn.append([label, int(sel.sum()), f"{values[sel & won].mean():+.3f}" if (sel & won).any() else "-",
                        f"{values[sel & ~won].mean():+.3f}" if (sel & ~won).any() else "-",
                        pct(((values[sel] > 0) == won[sel]).mean()), f"{auc(values[sel], won[sel]):.3f}",
                        f"{np.abs(values[sel] - returns[sel]).mean():.3f}"])
    overall_auc = auc(values, won)
    sign_accuracy = float(((values > 0) == won).mean())
    first = [(b.decisions[0]["value"], b.outcome == "win") for b in battles
             if b.decisions and b.outcome in ("win", "loss")]
    first_auc = auc([v for v, _ in first], [w for _, w in first])

    plt = pyplot()
    fig, ax = plt.subplots(figsize=(6.5, 4.2))
    means = [float(r[2]) for r in calibration]
    ax.plot([-1.2, 1.2], [-1.2, 1.2], color=INK["axis"], linewidth=1, zorder=1)
    ax.plot(means, [float(r[3]) for r in calibration], color=OURS, marker="o", markersize=7,
            markeredgecolor=INK["surface"], markeredgewidth=2, label="Mean realized return (the value's target)")
    ax.plot(means, [float(r[4]) for r in calibration], color=THEIRS, marker="o", markersize=7,
            markeredgecolor=INK["surface"], markeredgewidth=2, label="Mean outcome (+1 win, -1 loss)")
    ax.set_xlabel("Mean value in the bucket")
    ax.set_ylabel("Observed (same scale)")
    ax.set_xlim(-1.2, 1.2)
    ax.set_ylim(-1.2, 1.2)
    ax.set_title("Value calibration (the grey line is perfect calibration)")
    ax.legend(loc="upper left")
    save(fig, charts / "value_calibration.png")

    fig, ax = plt.subplots(figsize=(8, 3.6))
    max_turn = 1
    for outcome, color, name in [(True, OURS, "Won battles"), (False, THEIRS, "Lost battles")]:
        xs, ys = [], []
        for t in range(1, int(turns.max()) + 1):
            sel = (turns == t) & (won == outcome)
            if sel.sum() >= 15:
                xs.append(t)
                ys.append(values[sel].mean())
        max_turn = max(max_turn, max(xs, default=1))
        ax.plot(xs, ys, color=color, label=name)
    ax.axhline(0, color=INK["axis"], linewidth=1, zorder=1)
    ax.set_xlim(1, max(2, max_turn))
    ax.set_xlabel("Turn (decision points at that turn, 15+ per point)")
    ax.set_ylabel("Mean value")
    ax.set_title("Value by turn, won vs lost battles")
    ax.legend(loc="upper left", ncols=2)
    save(fig, charts / "value_by_turn.png")

    drops = []
    for b in battles:
        if b.outcome != "loss" or len(b.decisions) < 2:
            continue
        k = max(range(1, len(b.decisions)), key=lambda i: b.decisions[i - 1]["value"] - b.decisions[i]["value"])
        before, after = b.decisions[k - 1], b.decisions[k]
        drops.append({"battle": b.id, "url": b.url, "turn": after["turn"], "turn_before": before["turn"],
                      "forced_switch_after": after["force_switch"],
                      "value_before": round(before["value"], 3), "value_after": round(after["value"], 3),
                      "drop": round(before["value"] - after["value"], 3), "state_before": before["state"],
                      "state_after": after["state"],
                      "what_happened": summarize_events(b.lines, before["line"], after["line"], b.us)})
    drops.sort(key=lambda r: -r["drop"])
    drop_rows = [[link(r["battle"]), f"{r['turn_before']} to {r['turn']}"
                  + (" (forced switch)" if r["forced_switch_after"] else ""), f"{r['value_before']:+.2f}",
                  f"{r['value_after']:+.2f}", r["state_before"], r["what_happened"][:160]] for r in drops[:30]]
    text = ["## Value head", "",
            f"The value is the model's expected return: +-{WIN_REWARD:g} at the end, +-{FAINT_REWARD:g} per "
            f"Pokémon fainted on either side, discounted by {GAMMA} per decision (players.Rewards "
            f"\"shaped\"). The realized return below is that quantity computed from each battle. "
            f"{len(rows)} decision points from {len({b.id for _, b in rows})} decided battles.", "",
            table(["Value bucket", "Decisions", "Mean value", "Mean realized return", "Mean outcome",
                   "Won"], calibration), "",
            f"Over all decision points, the value's sign gets the result right {pct(sign_accuracy)} of the "
            f"time and separates won from lost battles with an AUC of {overall_auc:.3f}. "
            f"At the first decision of each battle (turn 1, before anything happened) the AUC is "
            f"{first_auc:.3f}.", "",
            "![Value calibration](charts/value_calibration.png)", "",
            table(["Turns", "Decisions", "Mean value (won)", "Mean value (lost)", "Sign right", "AUC",
                   "Mean abs(value - return)"], by_turn), "",
            "![Value by turn](charts/value_by_turn.png)", "",
            f"### Losses: where the value dropped the most ({len(drops)} losses, the 30 biggest drops; "
            f"all in value_drops.csv)", "",
            table(["Battle", "Turns", "Value before", "after", "State before (ours vs theirs)",
                   "What happened in between"], drop_rows, align="lrrrll"), ""]
    high, near_zero = values >= VALUE_EDGES[-1], (values >= 0) & (values < VALUE_EDGES[4])
    fit = calibrate(values, returns)
    text += ["### Calibration", "",
             f"A least-squares fit of the realized return on the value, over these {len(values)} decision "
             f"points: **expected return = {fit['scale']:.3f} x value {fit['offset']:+.3f}**. It takes the mean "
             f"gap from {fit['mean_gap_before']:+.3f} to {fit['mean_gap_after']:+.3f} and the mean absolute "
             f"error from {fit['mae_before']:.3f} to {fit['mae_after']:.3f}; it does not change the ranking "
             f"(AUC). Written to value_calibration.json; with --write-calibration, also next to the model, "
             f"where ElsaMina reads it (MiniPokeValueCalibration.cs) and logs the expected return with the "
             f"value. The model's play does not use the value.", ""]
    stats = {"auc": overall_auc, "sign": sign_accuracy, "first_auc": first_auc, "calibration": calibration,
             "fit": fit,
             "by_turn": by_turn, "drops": drops,
             "mean_gap": float(np.mean(values - returns)), "corr": float(np.corrcoef(values, returns)[0, 1]),
             "high_won": float(won[high].mean()) if high.any() else float("nan"), "high_n": int(high.sum()),
             "near_zero_won": float(won[near_zero].mean()) if near_zero.any() else float("nan")}
    return "\n".join(text), stats


def calibrate(values: np.ndarray, returns: np.ndarray) -> dict:
    """The affine map from value to realized return (least squares), and its effect."""
    if len(values) < 2 or np.ptp(values) == 0:
        scale, offset = 1.0, 0.0
    else:
        scale, offset = (float(x) for x in np.polyfit(values, returns, 1))
    calibrated = scale * values + offset
    return {"model": PLAYED_BY, "scale": round(scale, 6), "offset": round(offset, 6),
            "target": "realized shaped return (+-1 result, +-0.1 per faint, gamma 0.995)",
            "decisions": int(len(values)),
            "mean_gap_before": float(np.mean(values - returns)) if len(values) else float("nan"),
            "mean_gap_after": float(np.mean(calibrated - returns)) if len(values) else float("nan"),
            "mae_before": float(np.mean(np.abs(values - returns))) if len(values) else float("nan"),
            "mae_after": float(np.mean(np.abs(calibrated - returns))) if len(values) else float("nan")}


def summarize_events(lines: Sequence[str], start: int, end: int, our_side: str) -> str:
    """The moves, switches and faints between two lines, short."""
    out: List[str] = []

    def who(ident: str) -> str:
        side, name = mon_key(ident)
        return f"{'our' if side == our_side else 'their'} {name}"

    for line in lines[start + 1:end]:
        parts = line.split("|")
        if len(parts) < 3:
            continue
        kind = parts[1]
        if kind == "move" and len(parts) > 3:
            out.append(f"{who(parts[2])} {parts[3]}")
        elif kind in ("switch", "drag"):
            out.append(f"{who(parts[2])} in")
        elif kind == "faint":
            out.append(f"{who(parts[2])} fainted")
        elif kind == "-terastallize" and len(parts) > 3:
            out.append(f"{who(parts[2])} tera {parts[3]}")
        elif kind in ("-crit", "-miss", "-supereffective") and out:
            out[-1] += {"-crit": " (crit)", "-miss": " (miss)", "-supereffective": " (super effective)"}[kind]
        elif kind == "-status" and len(parts) > 3:
            out.append(f"{who(parts[2])} {parts[3]}")
    return "; ".join(out)


def behavior_section(battles: List[Battle], charts: Path) -> Tuple[str, dict]:
    decisions = {"us": 0, "them": 0}
    totals = {"us": Counter(), "them": Counter()}
    forced = {"us": 0, "them": 0}
    for b in battles:
        for who, side in (("us", b.us), ("them", b.them)):
            choices = b.replay.choices_of(side)
            decisions[who] += len(choices)
            beh = b.behavior[side]
            for name, _ in BEHAVIOR_COUNTS:
                totals[who][name] += behavior_count(beh, name)
            totals[who]["turn_choices"] += beh.turn_choices
            totals[who]["chosen_moves"] += beh.chosen_moves
            forced[who] += sum(c.kind == "switch" for c in choices) - beh.voluntary_switches

    def per100(who: str, name: str) -> float:
        return 100 * totals[who][name] / decisions[who] if decisions[who] else float("nan")

    rows = []
    for name, label in BEHAVIOR_COUNTS:
        rows.append([label, totals["us"][name], f"{per100('us', name):.2f}", totals["them"][name],
                     f"{per100('them', name):.2f}"])
    rows.insert(1, ["Forced switches (after a faint, U-turn, Eject Button...)", forced["us"],
                    f"{100 * forced['us'] / decisions['us']:.2f}", forced["them"],
                    f"{100 * forced['them'] / decisions['them']:.2f}"])
    mix = []
    for who, name in (("us", "Elsa-Mina"), ("them", "Opponents")):
        turn_choices = totals[who]["turn_choices"]
        switches = totals[who]["voluntary_switches"]
        mix.append([name, decisions[who], turn_choices, pct((turn_choices - switches) / turn_choices),
                    pct(switches / turn_choices), f"{(turn_choices - switches) / max(switches, 1):.1f}"])

    tera: Dict[str, List[int]] = {}
    tera_rows = []
    tera_stats: Dict[str, dict] = {}
    long_games = [b for b in battles if b.log.turns >= 5]
    for who, name in (("us", "Elsa-Mina"), ("them", "Opponents")):
        def side_of(b: Battle) -> SideBehavior:
            return b.behavior[b.us if who == "us" else b.them]

        used = [side_of(b).tera_turn for b in battles if side_of(b).tera_turn is not None]
        used_long = [b for b in long_games if side_of(b).tera_turn is not None]
        lefts = [side_of(b).tera_left for b in battles if side_of(b).tera_left is not None]
        ahead = sum(1 for o, t in lefts if o > t)
        behind = sum(1 for o, t in lefts if o < t)
        tera[who] = used
        tera_stats[who] = {"share": len(used) / len(battles), "median": float(np.median(used)) if used else None,
                           "turn1": sum(t <= 1 for t in used) / len(used) if used else 0.0,
                           "behind": behind / len(lefts) if lefts else 0.0}
        tera_rows.append([name, f"{len(used)} ({pct(len(used) / len(battles))})",
                          pct(len(used_long) / len(long_games)) if long_games else "n/a",
                          f"{np.median(used):.0f}" if used else "-", f"{np.mean(used):.1f}" if used else "-",
                          pct(sum(t <= 1 for t in used) / len(used)) if used else "-",
                          f"{ahead} / {len(lefts) - ahead - behind} / {behind}"])

    plt = pyplot()
    # voluntary switches are in the move/switch mix table: on this scale they hide the rest
    charted = [(name, label) for name, label in BEHAVIOR_COUNTS
               if not label.startswith("  ") and name != "voluntary_switches"]
    names = [label for _, label in charted]
    keys = [name for name, _ in charted]
    fig, ax = plt.subplots(figsize=(8, 0.55 * len(keys) + 1.2))
    y = np.arange(len(keys))
    ax.barh(y - 0.18, [per100("us", k) for k in keys], height=0.32, color=OURS, label="Elsa-Mina")
    ax.barh(y + 0.18, [per100("them", k) for k in keys], height=0.32, color=THEIRS, label="Opponents")
    ax.set_yticks(y, names)
    ax.invert_yaxis()
    ax.set_xlabel("Per 100 decisions (inputlog choices of that side)")
    ax.set_title("Behavior, Elsa-Mina vs its opponents in the same battles")
    ax.grid(axis="y", visible=False)
    ax.legend(loc="lower right")
    save(fig, charts / "behavior.png")

    tera_edges = [2, 3, 4, 6, 9, 13]
    tera_names = ["1", "2", "3", "4-5", "6-8", "9-12", "13+"]
    fig, ax = plt.subplots(figsize=(8, 3.4))
    x = np.arange(len(tera_names))
    for offset, who, color, name in [(-0.18, "us", OURS, "Elsa-Mina"), (0.18, "them", THEIRS, "Opponents")]:
        counts = Counter(bucket(t, tera_edges) for t in tera[who])
        ax.bar(x + offset, [counts.get(i, 0) / len(battles) for i in range(len(tera_names))], width=0.32,
               color=color, label=name)
    ax.set_xticks(x, tera_names)
    ax.yaxis.set_major_locator(plt.MultipleLocator(0.05))
    ax.yaxis.set_major_formatter(lambda v, _: f"{v:.0%}")
    ax.set_xlabel("Turn of the first terastallization")
    ax.set_ylabel("Share of battles")
    ax.set_title("When each side terastallizes")
    ax.grid(axis="x", visible=False)
    ax.legend(loc="upper left")
    save(fig, charts / "tera_turn.png")

    worst = []
    for name, label in BEHAVIOR_COUNTS:
        if name == "voluntary_switches":
            continue
        ranked = sorted((b for b in battles if behavior_count(b.behavior[b.us], name) > 0),
                        key=lambda b: (-behavior_count(b.behavior[b.us], name),
                                       -behavior_count(b.behavior[b.us], name) / max(1, len(b.replay.choices_of(b.us)))))
        if not ranked:
            continue
        items = []
        for b in ranked[:WORST]:
            beh = b.behavior[b.us]
            count = behavior_count(beh, name)
            if name == "switch_loops":
                example = "; ".join(f"turn {r['turn']}: {' > '.join(r['mons'])}" for r in beh.switch_loops[:2])
            elif name == "voluntary_switches":
                example = ""
            else:
                example = "; ".join(beh.examples[name][:2])
            items.append(f"{link(b.id)} ({count}{', ' + example if example else ''})")
        worst.append([label.strip(), "<br>".join(items)])

    def with_other_moves(name: str) -> Tuple[int, int]:
        """Of our repeats of this kind matched to the decision that chose the move, how many
        had another move legal (not locked in by a Choice item, Encore...)."""
        free = matched = 0
        for b in battles:
            for kind, turn, move in b.behavior[b.us].repeats:
                if kind != name:
                    continue
                d = next((d for d in b.decisions if d["status"] == "committed" and d["turn"] == turn
                          and d["choice"] and d["choice"].split()[:2] == ["move", move]), None)
                if d is not None:
                    matched += 1
                    free += int(d["mask"][6:10].sum() > 1)
        return free, matched

    free = {name: with_other_moves(name) for name in ("immune_repeated", "failed_repeated")}
    free_note = " ".join(
        f"Of Elsa-Mina's {label}, {n_free} of {n} (matched to their decision) were chosen while another "
        f"move was legal, so not locked in by a Choice item or Encore."
        for (n_free, n), label in ((free["immune_repeated"], "repeats into a known immunity"),
                                   (free["failed_repeated"], "repeated failures")) if n)

    text = ["## Behavior: Elsa-Mina vs the human opponents in the same battles", "",
            "Counted from the protocol for both sides. Rates are per 100 decisions of that side "
            "(its choices in the inputlog, forced switches included). A turn choice is a voluntary "
            "switch when that side's `|switch|` comes before any `|move|`, `|cant|` or `|faint|` of "
            "the turn; forced switches are the side's other switch choices. Results of a move "
            "(`|-immune|`, `|-fail|`) are counted for chosen moves only, not for moves called or "
            "locked in (`[from]`). Hazard redundancy uses the max layers (Stealth Rock and Sticky Web 1, "
            "Spikes 3, Toxic Spikes 2). Low HP is <= 33% before the move.", "",
            table(["Side", "Decisions", "Turn choices", "Moves", "Voluntary switches",
                   "Moves per voluntary switch"], mix), "",
            table(["Behavior", "Elsa-Mina", "per 100", "Opponents", "per 100"], rows), "",
            free_note, "",
            "![Behavior](charts/behavior.png)", "",
            "### Tera timing", "",
            table(["Side", "Battles with tera", "in battles reaching turn 5", "Median turn", "Mean turn",
                   "On turn 1", "Ahead / even / behind on Pokémon left"], tera_rows), "",
            "![Tera timing](charts/tera_turn.png)", "",
            f"### The {WORST} worst battles for each behavior (Elsa-Mina's count, then examples)", "",
            table(["Behavior", "Battles"], worst, align="ll"), ""]
    failed_moves = sum((b.behavior[b.us].failed_moves for b in battles), Counter())

    def most(name: str) -> List[Tuple[Battle, int]]:
        ranked = sorted(((b, behavior_count(b.behavior[b.us], name)) for b in battles), key=lambda t: -t[1])
        return [(b, n) for b, n in ranked if n > 0]

    stats = {"decisions": decisions, "totals": totals, "forced": forced, "per100": per100, "mix": mix,
             "tera": tera_rows, "tera_turns": tera, "tera_stats": tera_stats, "failed_moves": failed_moves,
             "most": most, "free": free}
    return "\n".join(text), stats


def comparison_section(battles: List[Battle], played: str, other_name: str) -> Tuple[str, dict]:
    decisions = [(d, b) for b in battles for d in b.decisions if "other_argmax" in d]
    if not decisions:
        return "", {}

    def kind(action: int) -> str:
        return "switch" if action < 6 else ("tera" if action >= 10 else "move")

    differ = np.array([d["argmax"] != d["other_argmax"] for d, _ in decisions])
    rate = differ.mean()
    rows = []

    def add(label: str, sel: np.ndarray) -> None:
        if sel.sum():
            rows.append([label, int(sel.sum()), pct(differ[sel].mean())])

    add("All decision points", np.ones(len(decisions), bool))
    add("Forced switches", np.array([d["force_switch"] for d, _ in decisions]))
    add("Turn decisions", np.array([not d["force_switch"] for d, _ in decisions]))
    by_kind = {}
    for k in ("move", "switch", "tera"):
        sel = np.array([not d["force_switch"] and kind(d["argmax"]) == k for d, _ in decisions])
        add(f"{played} chose a {k} (turn decisions)", sel)
        by_kind[k] = float(differ[sel].mean()) if sel.any() else float("nan")
    top = np.array([d["probs"].max() for d, _ in decisions])
    for lo, hi in ((0, 0.5), (0.5, 0.8), (0.8, 0.95), (0.95, 1.01)):
        add(f"{played}'s top probability {lo:g}-{min(hi, 1):g}", (top >= lo) & (top < hi))
    turns = np.array([d["turn"] for d, _ in decisions])
    for i, label in enumerate(turn_labels()):
        add(f"Turns {label}", np.array([bucket(t, TURN_EDGES) == i for t in turns]))
    transitions = Counter((kind(d["argmax"]), kind(d["other_argmax"])) for d, _ in decisions
                          if d["argmax"] != d["other_argmax"] and d["argmax"] >= 0 and d["other_argmax"] >= 0)

    per_battle = []
    for b in battles:
        ds = [d for d in b.decisions if "other_argmax" in d and d["status"] == "committed"]
        if ds and b.outcome in ("win", "loss") and not b.early_forfeit:
            per_battle.append((np.mean([d["argmax"] != d["other_argmax"] for d in ds]), b.outcome == "win",
                               len(ds), b))
    per_battle.sort(key=lambda t: t[0])
    if len(per_battle) < 3 or all(g[1] for g in per_battle) or not any(g[1] for g in per_battle):
        return (f"## {other_name} on the same states\n\nToo few decided battles of both results to compare.\n",
                {})
    thirds = np.array_split(np.arange(len(per_battle)), 3)
    tercile_rows, tercile_won = [], []
    for name, idx in zip(("Least disagreement", "Middle", "Most disagreement"), thirds):
        group = [per_battle[i] for i in idx]
        wins = sum(w for _, w, _, _ in group)
        tercile_won.append(wins / len(group))
        lo, hi = wilson(wins, len(group))
        tercile_rows.append([name, len(group), f"{pct(min(g[0] for g in group))} to {pct(max(g[0] for g in group))}",
                             f"{np.mean([g[2] for g in group]):.1f}", pct(wins / len(group)),
                             f"{pct(lo)} to {pct(hi)}"])
    win_rates = np.array([g[0] for g in per_battle if g[1]])
    loss_rates = np.array([g[0] for g in per_battle if not g[1]])
    lengths = np.array([g[2] for g in per_battle], float)
    rates = np.array([g[0] for g in per_battle])
    outcome = np.array([g[1] for g in per_battle], float)
    # does disagreement still go with losing once battle length is accounted for?
    design = np.column_stack([np.ones(len(rates)), rates, np.log(lengths)])
    coef = np.linalg.lstsq(design, outcome, rcond=None)[0]
    won = np.array([b.outcome == "win" for _, b in decisions])
    decided = np.array([b.outcome in ("win", "loss") for _, b in decisions])
    played_auc = auc(np.array([d["value"] for d, _ in decisions])[decided], won[decided])
    other_auc = auc(np.array([d["other_value"] for d, _ in decisions])[decided], won[decided])
    value_corr = float(np.corrcoef([d["value"] for d, _ in decisions], [d["other_value"] for d, _ in decisions])[0, 1])

    text = [f"## {other_name} on the same states", "",
            f"`{other_name}` (same encoder, its own spec and priors) evaluated on every decision "
            f"point of `{played}`'s battles. It would have chosen differently at "
            f"**{pct(rate)}** of them ({int(differ.sum())} of {len(decisions)}).", "",
            table(["Situation", "Decision points", f"{other_name} differs"], rows), "",
            "When they differ, " + ", ".join(f"{played} {a} -> {other_name} {b}: {n}"
                                               for (a, b), n in transitions.most_common()) + ".", "",
            f"Per battle (committed decisions, decided battles without early forfeits, {len(per_battle)} battles):",
            "", table(["Battles by disagreement rate", "Battles", "Disagreement", "Decisions per battle",
                       "Won", "95% interval"], tercile_rows), "",
            f"Mean disagreement {pct(win_rates.mean())} in wins vs {pct(loss_rates.mean())} in losses. "
            f"A linear probability fit of the result on the disagreement rate and log(decisions) gives "
            f"{coef[1]:+.2f} per unit of disagreement rate (so {coef[1] / 10:+.3f} win probability per 10 "
            f"points), with {coef[2]:+.2f} per log decision: longer battles are lost more often and have "
            f"more disagreement, so the raw tercile gap overstates it. Disagreement is not proof that "
            f"{other_name} would have done better.", "",
            f"Value heads: correlation {value_corr:.3f} between the two values; AUC for the result "
            f"{played_auc:.3f} ({played}) vs {other_auc:.3f} ({other_name}).", ""]
    stats = {"rate": rate, "n": len(decisions), "terciles": tercile_rows, "tercile_won": tercile_won,
             "coef": coef, "win_mean": win_rates.mean(), "loss_mean": loss_rates.mean(),
             "transitions": transitions, "by_kind": by_kind, "played_auc": played_auc, "other_auc": other_auc}
    return "\n".join(text), stats


def data_issues_section(result: dict) -> Tuple[str, dict]:
    battles: List[Battle] = result["battles"]
    text = ["## Data issues", ""]
    issues = result["pairing_issues"] + result["broken"]
    text += ([f"- {i}" for i in issues] if issues else ["Every file has its pair and every pair parsed."]) + [""]
    diff_rows = []
    for b in battles:
        n_choices = len(b.replay.choices_of(b.us))
        if len(b.decisions) == n_choices:
            continue
        status = Counter(d["status"] for d in b.decisions)
        why = []
        if status["rejected"]:
            why.append(f"{status['rejected']} rejected by |error|")
        if status["uncommitted"]:
            ending, side = b.ending
            who = "opponent" if side == b.them else "bot"
            why.append(f"{status['uncommitted']} never committed ({ending} by the {who} at turn {b.log.turns})"
                       if ending in ("forfeit", "inactivity") else f"{status['uncommitted']} never committed ({ending})")
        if b.leftover_choices:
            why.append(f"{b.leftover_choices} choices without a decision point")
        diff_rows.append([link(b.id), len(b.decisions), n_choices, "; ".join(why)])
    reasons = Counter()
    for row in diff_rows:
        reasons[re.sub(r" at turn \d+", "", re.sub(r"\d+ ", "", row[3]))] += 1
    text += [f"### Decision points vs our inputlog choices ({len(diff_rows)} battles differ)", "",
             "Decision points are poke-env's choose_move calls (every `|request|` but `wait` ones). "
             "They differ from our inputlog choices when Showdown rejected a choice (it asks again) "
             "or when the battle ended before our last choice was committed (Showdown only logs a "
             "turn's choices once both sides chose).", "",
             table(["Reason", "Battles"], [[r, n] for r, n in reasons.most_common()], align="lr"), ""]
    if diff_rows:
        text += ["<details><summary>Every battle that differs</summary>", "",
                 table(["Battle", "Decision points", "Our choices", "Why"], diff_rows, align="lrrl"), "",
                 "</details>", ""]
    parse_errors = [(b, e) for b in battles for e in b.parse_errors]
    text += [f"### poke-env exceptions while replaying: {len(parse_errors)}", ""]
    for b, (line, message) in parse_errors[:30]:
        text.append(f"- {link(b.id)} line {line + 1}: `{message}`")
    messages = Counter(re.sub(r"\d+", "N", m)[:100] for b in battles for m in b.messages)
    if messages:
        text += ["", "poke-env log messages (level 25 and up) while replaying:", "",
                 table(["Message", "Count"], [[f"`{m}`", n] for m, n in messages.most_common(15)], align="lr")]
    # our voluntary switches: protocol rule vs requests
    log_rule = sum(b.behavior[b.us].voluntary_switches for b in battles)
    from_requests = sum(1 for b in battles for d in b.decisions if d["status"] == "committed"
                        and not d["force_switch"] and d["choice"] and d["choice"].startswith("switch"))
    text += ["", f"Check of the behavior section's switch rule on our side: {log_rule} voluntary switches "
             f"from the protocol vs {from_requests} switch choices at non-forced requests.", ""]
    inconsistent = [(b, d) for b in battles for d in b.decisions if d["status"] == "committed" and d["choice"]
                    and ((d["choice"].startswith("move") and d["force_switch"])
                         or (d["choice"].startswith("switch") and d["trapped"]))]
    text += [f"Alignment check: {len(inconsistent)} committed choices that do not fit their request "
             f"(a move at a forced switch, or a switch while the request says trapped)."]
    for b, d in inconsistent[:20]:
        text.append(f"- {link(b.id)} turn {d['turn']}: `{d['choice']}` at a "
                    f"{'forced switch' if d['force_switch'] else 'turn'} request")
    stats = {"diff_rows": diff_rows, "parse_errors": len(parse_errors), "reasons": reasons,
             "inconsistent": len(inconsistent), "switch_check": (log_rule, from_requests)}
    return "\n".join(text + [""]), stats


def guard_finding(g: dict) -> str:
    counts = g["counts"]
    parts = ", ".join(f"{counts[(r, False)]} {GUARD_REASONS[r]}" for r in GUARD_REASONS if counts[(r, False)])
    played = (f" {g['battles']} battles were played with it." if g["battles"]
              else " No battle here was played with it yet.")
    return (f" ElsaMina's guard would have changed {g['without']} of the {g['decisions_without']} decisions "
            f"of the battles played without it" + (f" ({parts})" if parts else "") + "." + played)


def findings(battles: List[Battle], s: dict) -> List[str]:
    """The findings that matter most, as sentences with their numbers and links."""
    res, end, par, val, beh, cmp_ = (s["results"], s["endings"], s["parity"], s["value"], s["behavior"],
                                     s["compare"])
    main = [b for b in battles if b.played_by == PLAYED_BY]
    name = f"`{strategy(PLAYED_BY)}`"
    out = []

    others = par["others"]
    parity = (f"**Parity {pct(par['rate'], 2)}** ({par['matched']} of {par['committed']} committed choices are "
              f"the argmax of the model that played the battle, on poke-env's replay of the log)")
    if par["committed"] > par["matched"]:
        parity += ("; the others: " + ", ".join(f"{n} {c}" for c, n in
                                                sorted(par["by_cause"].items(), key=lambda kv: -kv[1])) + ".")
    else:
        parity += ": ElsaMina's C# tracker and encoder agree with poke-env and encoder_v2.py on every state played."
    if others:
        starts = sorted(b.log.start_time for b in others if b.log.start_time)
        n_choices = sum(sum(d["status"] == "committed" and d["choice"] is not None for d in b.decisions)
                        for b in others)
        explained = sum(b.model_matches.get(PLAYED_BY, 0) for b in others)
        parity = (f"**Not every battle is {name}'s.** The last {len(others)} ({fmt_time(starts[0])} to "
                  f"{fmt_time(starts[-1])}) were played by "
                  + ", ".join(sorted({f"`{strategy(b.played_by)}`" for b in others}))
                  + f": {name} explains only {pct(explained / n_choices)} of their {n_choices} choices. "
                  f"The model-specific sections below leave them out. " + parity)
    out.append(parity)

    n, wins = res["n"], res["wins"]
    given = sum(b.outcome == "win" and b.ending[0] in ("forfeit", "inactivity") for b in main)
    out.append(f"**{name} went {res['all'][2]} ({res['all'][3]}, 95% interval {res['all'][4]})**, and "
               f"{given} of its {wins} wins are opponent forfeits or timeouts. In the {res['to_the_end'][1]} "
               f"battles played to the last Pokémon it won {res['to_the_end'][3]} ({res['to_the_end'][4]}); "
               f"humans forfeit lost positions, so that subset is biased against the bot, but most of its "
               f"wins are not taken on the board. Without forfeits before turn {EARLY_FORFEIT_TURN}: "
               f"{res['no_early'][3]}.")

    buckets = res.get("buckets") or []
    behind = [r for r in buckets if r[1] < r[4]]
    if buckets:
        out.append(f"**Rating {res['start_rating']} to {res['final'][1]}** (peak {res['peak'][1]}), performance "
                   f"rating **{res['perf']:.0f}**: {res['actual']:.1f} points scored where Elo expected "
                   f"{res['expected']:.1f} over {res['n_rated']} rated battles. The score goes from "
                   f"{pct(buckets[0][1], 0)} against {buckets[0][0]} opponents to {pct(buckets[-1][1], 0)} "
                   f"against {buckets[-1][0]}, below the Elo expectation in "
                   + (", ".join(r[0] for r in behind) if behind else "no bucket") + ".")

    drops = val["drops"][:2]
    tone = "optimistic" if val["mean_gap"] > 0.05 else ("pessimistic" if val["mean_gap"] < -0.05 else "calibrated")
    fit = val["fit"]
    out.append(f"**The value head is {tone} against humans**: it exceeds the realized return by "
               f"{val['mean_gap']:+.2f} on average; decisions valued {VALUE_EDGES[-1]:+.2f} and up were won only "
               f"{pct(val['high_won'])} of the time, those between 0 and {VALUE_EDGES[4]:+.2f} "
               f"{pct(val['near_zero_won'])}. It still ranks positions (AUC {val['auc']:.3f} for the result, "
               f"{val['first_auc']:.3f} on turn 1). Calibrated, expected return = {fit['scale']:.2f} x value "
               f"{fit['offset']:+.2f} (mean gap {fit['mean_gap_after']:+.2f})."
               + (" Biggest drops in losses: " + "; ".join(
                   f"{link(r['battle'])} turn {r['turn_before']} to {r['turn']} "
                   f"({r['value_before']:+.2f} to {r['value_after']:+.2f})" for r in drops) + "." if drops else ""))

    per100, totals, most = beh["per100"], beh["totals"], beh["most"]
    repeat_links = ", ".join(f"{link(b.id)} ({n}x, {b.behavior[b.us].examples['immune_repeated'][0]})"
                             for b, n in most("immune_repeated")[:2])
    out.append(f"**Moves into an immunity: {per100('us', 'immune'):.2f} per 100 decisions vs "
               f"{per100('them', 'immune'):.2f} for the humans**, {per100('us', 'immune_ability'):.2f} vs "
               f"{per100('them', 'immune_ability'):.2f} into an ability (Levitate, Earth Eater, Water Absorb...). "
               f"{totals['us']['immune_repeated']} times it repeated a move into a target already shown immune to "
               f"it (humans: {totals['them']['immune_repeated']}), {beh['free']['immune_repeated'][0]} of "
               f"{beh['free']['immune_repeated'][1]} with another move legal"
               + (f": {repeat_links}." if repeat_links else ".")
               + guard_finding(par["guard"]))

    failed = beh["failed_moves"]
    worst_failed = most("failed_repeated")[:1]
    out.append(f"**Failed moves: {per100('us', 'failed'):.2f} vs {per100('them', 'failed'):.2f} per 100**, "
               f"{totals['us']['failed_repeated']} of them the same Pokémon's move failing again "
               f"(humans: {totals['them']['failed_repeated']}; {beh['free']['failed_repeated'][0]} of "
               f"{beh['free']['failed_repeated'][1]} with another move legal). Most failed: "
               + ", ".join(f"{(MOVES.get(m) or {}).get('name', m)} {k}" for m, k in failed.most_common(4))
               + (f"; worst: {link(worst_failed[0][0].id)} "
                  f"({worst_failed[0][0].behavior[worst_failed[0][0].us].examples['failed'][0]}, "
                  f"{worst_failed[0][0].behavior[worst_failed[0][0].us].failed} failures)" if worst_failed else "")
               + ".")

    tera = beh["tera_stats"]
    median = lambda who: "-" if tera[who]["median"] is None else f"{tera[who]['median']:.0f}"
    out.append(f"**Tera timing**: in {pct(tera['us']['share'], 0)} of its battles (humans "
               f"{pct(tera['them']['share'], 0)}), median turn {median('us')} vs "
               f"{median('them')}, {pct(tera['us']['turn1'], 0)} on turn 1 (humans "
               f"{pct(tera['them']['turn1'], 0)}), and {pct(tera['us']['behind'], 0)} of the time while behind on "
               f"Pokémon left (humans {pct(tera['them']['behind'], 0)}).")

    counts = end["counts"]
    errors = end["errors"]
    sentence = (f"**No bot forfeit or timeout in {len(battles)} battles** ({counts.get(('forfeit', 'opponent'), 0)} "
                f"opponent forfeits, {counts.get(('inactivity', 'opponent'), 0)} opponent timeouts)."
                if not end["ours"] else
                f"**{len(end['ours'])} battles ended by the bot itself**: "
                + ", ".join(link(b.id) for b in end["ours"][:5]) + ".")
    if errors:
        repeated = [row[0] for row in errors if sum(r[0] == row[0] for r in errors) > 1]
        sentence += (f" Its only rejected choices are {len(errors)} switches out of a trapped Pokémon: the "
                     f"requests said `maybeTrapped` (an unrevealed Shadow Tag, Arena Trap or Magnet Pull), which "
                     f"leaves switches in poke-env's mask"
                     + (f"; in {repeated[0]} it tried the same switch on the next turn too" if repeated else "")
                     + ".")
    out.append(sentence)

    low, repeat = totals["us"]["setup_low_hp"], totals["us"]["setup_low_hp_repeated"]
    if low:
        out.append(f"**Setup or Protect at <=33% HP is rare ({per100('us', 'setup_low_hp'):.2f} per 100 vs "
                   f"{per100('them', 'setup_low_hp'):.2f} for humans), but {repeat} of its {low} are repeats** "
                   f"(the previous move was setup or Protect too), e.g. "
                   + ", ".join(f"{link(b.id)} ({b.behavior[b.us].examples['setup_low_hp_repeated'][0]})"
                               for b, _ in most("setup_low_hp_repeated")[:2]) + ".")

    if cmp_:
        model_rows = {row[0]: row for row in res["models"]}
        other_row = model_rows.get(f"`{strategy(COMPARE_WITH)}`")
        won = cmp_["tercile_won"]
        out.append(f"**`{strategy(COMPARE_WITH)}` would have chosen differently at {pct(cmp_['rate'])} of "
                   f"{name}'s decisions**, most at tera ({pct(cmp_['by_kind']['tera'], 0)}) and switch "
                   f"({pct(cmp_['by_kind']['switch'], 0)}) decisions. Battles with more disagreement were not "
                   f"lost more often (won {pct(won[0], 0)} / {pct(won[1], 0)} / {pct(won[2], 0)} from least to most). "
                   f"Its value ranks results a little better (AUC {cmp_['other_auc']:.3f} vs "
                   f"{cmp_['played_auc']:.3f})"
                   + (f"; its own {other_row[1]} battles went {other_row[2]}." if other_row else "."))
    return out[:10]


# --------------------------------------------------------------------------
# 10. Outputs
# --------------------------------------------------------------------------
def write_csv(path: Path, rows: List[dict]) -> None:
    if not rows:
        path.write_text("")
        return
    with path.open("w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def battle_row(b: Battle) -> dict:
    us, them = b.behavior[b.us], b.behavior[b.them]
    committed = [d for d in b.decisions if d["status"] == "committed"]
    row = {
        "id": b.id, "url": b.url, "start": fmt_time(b.log.start_time), "upload": fmt_time(b.replay.upload_time),
        "played_by": strategy(b.played_by), "attribution": b.attribution,
        **{f"{strategy(m)}_explains": n for m, n in b.model_matches.items()},
        "our_side": b.us, "opponent": b.log.names.get(b.them, ""), "rated": b.rated,
        "rating_before": b.rating(b.us), "opponent_rating_before": b.rating(b.them),
        "rating_after": b.rating_after(b.us), "opponent_rating_after": b.rating_after(b.them),
        "outcome": b.outcome, "ending": b.ending[0], "ended_by": b.ended_by or "",
        "early_forfeit": b.early_forfeit, "turns": b.log.turns,
        "mons_left": b.log.mons_left(b.us), "opponent_mons_left": b.log.mons_left(b.them),
        "decision_points": len(b.decisions), "our_choices": len(b.replay.choices_of(b.us)),
        "opponent_choices": len(b.replay.choices_of(b.them)),
        "rejected": sum(d["status"] == "rejected" for d in b.decisions),
        "uncommitted": sum(d["status"] == "uncommitted" for d in b.decisions),
        "parity_matches": sum(d["check"] in ("match", "tie") for d in committed),
        "parity_mismatches": sum(d["check"] in ("mismatch", "illegal", "unmapped") for d in committed),
        "errors": len(b.log.errors),
        "guard": b.guard, "guard_attribution": b.guard_attribution,
        "search": b.search, "search_attribution": b.search_attribution,
        "guard_changes": sum(bool(d.get("guard_override")) for d in b.decisions),
        "first_value": round(b.decisions[0]["value"], 4) if b.decisions else "",
        "min_value": round(min(d["value"] for d in b.decisions), 4) if b.decisions else "",
        "tera_turn": us.tera_turn if us.tera_turn is not None else "",
        "opponent_tera_turn": them.tera_turn if them.tera_turn is not None else "",
    }
    if len(b.model_matches) > 1:
        other = [d for d in committed if "other_argmax" in d]
        row["other_model"] = strategy(next(m for m in b.model_matches if m != b.played_by))
        row["other_model_disagreement"] = (round(float(np.mean([d["argmax"] != d["other_argmax"] for d in other])), 4)
                                           if other else "")
    for name, _ in BEHAVIOR_COUNTS:
        row[name] = behavior_count(us, name)
        row[f"opponent_{name}"] = behavior_count(them, name)
    return row


def decision_line(b: Battle, d: dict, k: int) -> dict:
    top = [int(a) for a in np.argsort(-d["probs"])[:3] if d["mask"][a]]
    out = {
        "battle": b.id, "model": strategy(b.played_by), "index": d["index"], "decision": k, "turn": d["turn"],
        "line": d["line"] + 1,
        "rqid": d["rqid"], "status": d["status"], "kind": "forced switch" if d["force_switch"] else "turn",
        "trapped": d["trapped"], "can_tera": d["can_tera"], "state": d["state"],
        "choice": d["choice"], "action": d["action"], "mapping_problem": d["mapping_problem"],
        "check": d["check"], "cause": d.get("cause"), "argmax": d["argmax"],
        "guard_vetoes": {str(a): r for a, r in sorted(d["vetoes"].items())},
        "guard_override": d.get("guard_override"),
        "argmax_action": describe_action(d["argmax"], d),
        "mask": [int(x) for x in d["mask"]], "probs": [round(float(p), 5) for p in d["probs"]],
        "top": [[describe_action(a, d), round(float(d["probs"][a]), 4)] for a in top],
        "value": round(d["value"], 5), "entropy": round(d["entropy"], 5), "return": round(d["return"], 5),
        "outcome": b.outcome, "fainted": d["fainted"], "team": d["team"], "moves": d["moves"],
    }
    if "other_argmax" in d:
        out["compare"] = {"model": strategy(d["other_model"]), "argmax": d["other_argmax"],
                          "argmax_action": describe_action(d["other_argmax"], d),
                          "probs": [round(float(p), 5) for p in d["other_probs"]],
                          "value": round(d["other_value"], 5)}
    return out


def write_outputs(result: dict, out: Path) -> Path:
    battles: List[Battle] = result["battles"]
    main = [b for b in battles if b.played_by == PLAYED_BY]   # the model this report is about
    charts = out / "charts"
    charts.mkdir(parents=True, exist_ok=True)
    sections, stats = {}, {}
    sections["results"], stats["results"] = results_section(battles, charts)
    sections["endings"], stats["endings"] = endings_section(battles)
    sections["parity"], stats["parity"] = parity_section(battles, result["models"])
    sections["value"], stats["value"] = value_section(main, charts)
    sections["behavior"], stats["behavior"] = behavior_section(main, charts)
    sections["compare"], stats["compare"] = (comparison_section(main, PLAYED_BY, COMPARE_WITH)
                                             if COMPARE_WITH in result["models"] else ("", {}))
    sections["issues"], stats["issues"] = data_issues_section(result)

    starts = [b.log.start_time for b in battles if b.log.start_time]
    uploads = [b.replay.upload_time for b in battles if b.replay.upload_time]
    split = Counter(b.played_by for b in battles)
    head = [f"# {strategy(PLAYED_BY)} against humans on the ladder", "",
            f"{len(battles)} gen9randombattle battles saved with -savereplays, started {fmt_time(min(starts))} "
            f"to {fmt_time(max(starts))} (uploaded {fmt_time(min(uploads))} to {fmt_time(max(uploads))}), "
            f"from `{result['folder']}`: "
            + ", ".join(f"{n} played by `{strategy(m)}`" for m, n in split.most_common())
            + f" (from their choices, see Parity). Every battle was replayed into poke-env "
            f"{version('poke-env')} and evaluated with {', '.join('`' + m + '`' for m in result['models'])}. "
            f"Generated {datetime.now().astimezone().strftime('%Y-%m-%d %H:%M %Z')} by analyze_replays.py "
            f"in {result['seconds']:.0f}s. Replay links open the public replays.", "",
            "## Key findings", ""]
    head += [f"{i}. {f}" for i, f in enumerate(findings(battles, stats), 1)] + [""]
    report = "\n".join(head + [sections[k] for k in ("results", "endings", "parity", "value", "behavior",
                                                      "compare", "issues") if sections[k]])
    (out / "report.md").write_text(report, encoding="utf-8")

    write_csv(out / "battles.csv", [battle_row(b) for b in battles])
    (out / "value_calibration.json").write_text(json.dumps(stats["value"]["fit"], indent=1) + "\n")
    write_csv(out / "value_drops.csv", stats["value"]["drops"])
    with (out / "decisions.jsonl").open("w", encoding="utf-8") as f:
        for b in battles:
            for k, d in enumerate(b.decisions):
                f.write(json.dumps(decision_line(b, d, k), separators=(",", ":")) + "\n")
    return out / "report.md"


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("replays", type=Path, help="folder with the <id>.log / <id>.json pairs (only read)")
    ap.add_argument("--out", type=Path, default=Path("replays/analysis"))
    ap.add_argument("--no-compare", action="store_true", help=f"skip {COMPARE_WITH}")
    ap.add_argument("--refresh", action="store_true", help="replay every battle again, ignoring the cache")
    ap.add_argument("--limit", type=int, help="only the first N battles (by id)")
    ap.add_argument("--write-calibration", action="store_true",
                    help=f"also write value_calibration.json into {PLAYED_BY}'s model folder, where "
                         f"ElsaMina reads it")
    args = ap.parse_args()
    if not args.replays.is_dir():
        ap.error(f"{args.replays} is not a folder")
    if args.out.resolve() == args.replays.resolve() or args.replays.resolve() in args.out.resolve().parents:
        ap.error("--out must not be inside the replay folder (it is only read)")
    args.out.mkdir(parents=True, exist_ok=True)
    result = analyze(args.replays, args.out, args.refresh, not args.no_compare, args.limit)
    print(f"wrote {write_outputs(result, args.out)}")
    if args.write_calibration:
        target = MODELS_DIR / PLAYED_BY / "value_calibration.json"
        target.write_bytes((args.out / "value_calibration.json").read_bytes())
        print(f"wrote {target}")


if __name__ == "__main__":
    main()
