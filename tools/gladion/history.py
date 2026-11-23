"""
What happened earlier in a battle, read from its protocol log.

poke-env keeps every battle line it parsed, split on "|", in `battle._replay_data`
(`|request|` is not among them). These helpers only look at a few line types:

    |turn|N                       turn boundaries
    |move|pXa: Name|Move|...      a move used; lines with a "[from]..." tag after the
                                  move are not choices (Outrage's lock, Sleep Talk's
                                  call, Magic Bounce...) and are skipped
    |switch| |drag| |replace|     a Pokémon coming in (|drag|: forced out by Roar...)
    |faint|pXa: Name

"The previous turn" is the lines between the last two |turn| lines: a decision
taken mid-turn (a replacement after a faint) still looks at the last complete turn.

move_outcomes() reads what came of every chosen move (the `move_effects` features,
effects.py): a |-fail| line after it marks it failed, an |-immune| line on the other
side records that the move had no effect on that Pokémon, until the next |move|,
|switch|, |drag|, |replace|, |turn|, |cant| or |upkeep| line.

recent_events() turns the log into events for the history tokens: every |move|
(called and locked moves included, flagged) and every |switch| / |drag|, with the
results that follow it until the next event or |turn|:

    |-crit| |-miss| |-supereffective| |-resisted| |-immune| |-fail|   flags of the move
    |faint|pXa: ...       "KO" on the last move of that turn whose user is on the other
                          side, unless that side brought a Pokémon in after it
    [miss] [still] [notarget] after a |move| line: miss / fail
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, List, Optional, Sequence, Tuple

from poke_env.data import to_id_str

Line = Sequence[str]
ENTRANCES = ("switch", "drag", "replace")
RESULT_FLAGS = {"-crit": "crit", "-miss": "miss", "-supereffective": "supereffective",
                "-resisted": "resisted", "-immune": "immune", "-fail": "fail"}


def lines_of(battle) -> List[Line]:
    return battle._replay_data


def side_of(ident: str) -> str:
    """"p2a: Venusaur" -> "p2"."""
    return ident[:2]


def is_chosen_move(line: Line) -> bool:
    """A |move| line the Pokémon chose (not locked into, called, or reflected)."""
    return (len(line) > 3 and line[1] == "move"
            and not any(part.startswith("[from]") for part in line[4:]))


def previous_turn(lines: Sequence[Line]) -> Sequence[Line]:
    """Lines between the last two |turn| lines (empty before turn 2)."""
    turns = [i for i, line in enumerate(lines) if len(line) > 1 and line[1] == "turn"]
    if len(turns) < 2:
        return []
    return lines[turns[-2] + 1: turns[-1]]


def switched_by_choice(turn_lines: Sequence[Line], side: str) -> bool:
    """`side` switched instead of moving: its |switch| comes before any |move| or
    |faint| of that side in the turn (a U-turn switch or a replacement after a
    faint comes after one of those)."""
    for line in turn_lines:
        if len(line) < 3 or side_of(line[2]) != side:
            continue
        if line[1] in ("move", "faint"):
            return False
        if line[1] == "switch":
            return True
    return False


def last_chosen_moves(lines: Sequence[Line], side: str, n: int = 2) -> List[str]:
    """The last `n` moves `side`'s active Pokémon chose since it came in, newest
    first (Struggle excluded)."""
    out: List[str] = []
    for line in reversed(lines):
        if len(line) < 3 or side_of(line[2]) != side:
            continue
        if line[1] in ENTRANCES:
            break
        if is_chosen_move(line) and line[3] != "Struggle":
            out.append(line[3])
            if len(out) == n:
                break
    return out


def same_move_twice(lines: Sequence[Line], side: str) -> bool:
    moves = last_chosen_moves(lines, side, 2)
    return len(moves) == 2 and moves[0] == moves[1]


def active_turns(mon) -> int:
    """poke-env's turns-on-the-field counter (V1's first_turn reads it too): +1 at
    every |turn|, 0 on switching out."""
    return getattr(mon, "_active_turns", 0) if mon is not None else 0


def opponent_role(battle) -> Optional[str]:
    role = battle.player_role
    return None if role is None else ("p2" if role == "p1" else "p1")


@dataclass
class Event:
    side: str                      # "p1" / "p2"
    species: str                   # species id of the Pokémon that moved / came in
    move: Optional[str]            # move id, None for an entrance
    turn: int                      # turn number when it happened (0 before turn 1)
    drag: bool = False             # forced in (|drag|)
    called: bool = False           # |move| with a [from] tag (locked, called, reflected)
    flags: Dict[str, bool] = field(default_factory=dict)   # RESULT_FLAGS values + "ko"


def _name(ident: str) -> Tuple[str, str]:
    """"p2a: Iron Valiant" -> ("p2", "Iron Valiant")."""
    side, _, name = ident.partition(":")
    return side[:2], name.strip()


def recent_events(lines: Sequence[Line], k: int) -> List[Event]:
    """The last `k` events of the log, oldest first."""
    species: Dict[Tuple[str, str], str] = {}
    events: List[Event] = []
    turn = 0
    for line in lines:
        if len(line) < 2:
            continue
        kind = line[1]
        if kind == "turn" and len(line) > 2:
            turn = int(line[2])
        elif kind in ENTRANCES and len(line) > 3:
            side, name = _name(line[2])
            species[(side, name)] = to_id_str(line[3].split(",")[0])
            if kind != "replace":  # Illusion ending: a reveal, not an entrance
                events.append(Event(side, species[(side, name)], None, turn, drag=kind == "drag"))
        elif kind == "move" and len(line) > 3:
            side, name = _name(line[2])
            tags = line[4:]
            ev = Event(side, species.get((side, name), to_id_str(name)), to_id_str(line[3]), turn,
                       called=any(t.startswith("[from]") for t in tags))
            if "[miss]" in tags:
                ev.flags["miss"] = True
            if "[still]" in tags or "[notarget]" in tags:
                ev.flags["fail"] = True
            events.append(ev)
        elif kind in RESULT_FLAGS and events and events[-1].move is not None:
            events[-1].flags[RESULT_FLAGS[kind]] = True
        elif kind == "faint" and len(line) > 2:
            fainted_side, _ = _name(line[2])
            for ev in reversed(events):
                if ev.turn != turn or (ev.move is None and ev.side == fainted_side):
                    break  # an earlier turn, or it came in after (hazards...)
                if ev.move is not None and ev.side != fainted_side:
                    ev.flags["ko"] = True
                    break
    return events[-k:] if k > 0 else []


OUTCOME_END = ("move", "switch", "drag", "replace", "turn", "cant", "upkeep")


@dataclass
class MoveOutcomes:
    # (side, name, move id) -> whether each chosen use failed, oldest first
    uses: Dict[Tuple[str, str, str], List[bool]] = field(default_factory=dict)
    # (user side, move id, target side, target name) -> index of its last |-immune| line
    immune: Dict[Tuple[str, str, str, str], int] = field(default_factory=dict)
    # (side, name) -> index of its last |-terastallize| line
    tera: Dict[Tuple[str, str], int] = field(default_factory=dict)

    def failed_last(self, side: str, name: str, move: str, n: int) -> bool:
        """The last `n` uses of `move` by that Pokémon all failed."""
        uses = self.uses.get((side, name, move), [])
        return len(uses) >= n and all(uses[-n:])

    def immune_before(self, side: str, move: str, target_side: str, target_name: str) -> bool:
        """`side`'s `move` had no effect on that Pokémon since it last terastallized."""
        line = self.immune.get((side, move, target_side, target_name))
        return line is not None and line > self.tera.get((target_side, target_name), -1)


def move_outcomes(lines: Sequence[Line]) -> MoveOutcomes:
    out = MoveOutcomes()
    pending: Optional[Tuple[str, str, str]] = None
    for i, line in enumerate(lines):
        if len(line) < 2:
            continue
        kind = line[1]
        if kind in OUTCOME_END:
            pending = None
        if kind == "move" and is_chosen_move(line):
            side, name = _name(line[2])
            move = to_id_str(line[3])
            if move != "struggle":
                pending = (side, name, move)
                out.uses.setdefault(pending, []).append(False)
        elif kind == "-fail" and pending is not None:
            out.uses[pending][-1] = True
        elif kind == "-immune" and pending is not None and len(line) > 2:
            target_side, target_name = _name(line[2])
            if target_side != pending[0]:
                out.immune[(pending[0], pending[2], target_side, target_name)] = i
        elif kind == "-terastallize" and len(line) > 2:
            out.tera[_name(line[2])] = i
    return out
