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
"""

from __future__ import annotations

from typing import List, Optional, Sequence

Line = Sequence[str]
ENTRANCES = ("switch", "drag", "replace")


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
