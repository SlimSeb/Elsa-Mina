"""
A deterministic, simplified gen 9 damage estimate for the matchup tokens. It is
meant to be ported line by line (encoder_spec.json repeats these rules), not to be
exact: no abilities except Adaptability, no items except a revealed Choice Scarf
(speed), no terrain, no screens other than Reflect / Light Screen / Aurora Veil.

Stats. Ours: the request's stats and HP. Theirs (unknown): random battle spreads,
84 EVs and 31 IVs in every stat, neutral nature:
    HP   = floor((2 * base + 31 + 21) * level / 100) + level + 10
    stat = floor((2 * base + 31 + 21) * level / 100) + 5
and current HP = round(HP * hp fraction). A boost stage s multiplies a stat by
(2 + s) / 2 for s >= 0, 2 / (2 - s) below.

Damage of a move (0 for status moves and for moves whose base power is 0 in the dex,
Low Kick and the like, except the fixed ones):
    Seismic Toss, Night Shade (dex damage "level"): the attacker's level
    Super Fang, Ruination, Nature's Madness: half the defender's current HP
    otherwise:
      A = attacker's Atk (physical) or SpA (special) with boosts; Body Press uses its
          Def, Foul Play the defender's Atk (with the defender's Atk boosts)
      D = defender's Def (physical, Psyshock-like moves) or SpD, with boosts
      base = floor(floor(floor(2 * level / 5 + 2) * power * A / D) / 50) + 2
      damage = base * STAB * effectiveness * burn * weather * screen * crit * 0.925 * hits
    STAB 1.5 when the move's type is one of the attacker's own types (poke-env
    base_types) or its tera type once terastallized; 2 when terastallized into one of
    its own types and the move has that type; Adaptability: 2 (2.25 for that tera case).
    effectiveness: poke-env Pokemon.damage_multiplier (V1's move features use it too).
    burn 0.5 for a physical move of a burned attacker. Sun: Fire 1.5, Water 0.5; rain:
    Water 1.5, Fire 0.5. Reflect (physical) / Light Screen (special) / Aurora Veil on
    the defender's side: 0.5. Moves that always crit (dex willCrit): 1.5. 0.925: the
    mean damage roll. hits: 3.1 for 2-5 hit moves, else the dex's hit count (mean).

expected fraction = accuracy * min(damage, defender's current HP) / defender's HP
KO possible       = damage >= defender's current HP (before accuracy)
Speed: Spe with boosts, x0.5 paralyzed, x1.5 with a revealed Choice Scarf, x2 under
the side's Tailwind; under Trick Room the slower one moves first.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Dict, Optional

from poke_env.battle import (Battle, Field, Move, MoveCategory, Pokemon, PokemonType,
                             SideCondition, Status, Weather)

STATS = ("atk", "def", "spa", "spd", "spe")
HALF_HP_MOVES = ("superfang", "ruination", "naturesmadness")
MEAN_ROLL = 0.925
SUN = (Weather.SUNNYDAY, Weather.DESOLATELAND)
RAIN = (Weather.RAINDANCE, Weather.PRIMORDIALSEA)


def boost_multiplier(stage: int) -> float:
    return (2 + stage) / 2 if stage >= 0 else 2 / (2 - stage)


def random_battle_stat(base: int, level: int, hp: bool = False) -> int:
    core = (2 * base + 31 + 21) * level // 100
    return core + level + 10 if hp else core + 5


@dataclass
class Fighter:
    """What the estimate needs about one Pokémon."""
    mon: Pokemon
    stats: Dict[str, int]
    max_hp: int
    hp: float

    @classmethod
    def of(cls, mon: Pokemon, ours: bool) -> "Fighter":
        level = mon.level or 100
        base = mon.base_stats or {}
        if ours and mon.max_hp and all(mon.stats.get(s) for s in STATS):
            stats = {s: int(mon.stats[s]) for s in STATS}
            max_hp, hp = mon.max_hp, float(mon.current_hp)
        else:
            stats = {s: random_battle_stat(base.get(s, 0), level) for s in STATS}
            max_hp = random_battle_stat(base.get("hp", 0), level, hp=True)
            hp = float(round(max_hp * (mon.current_hp_fraction or 0.0)))
        return cls(mon, stats, max_hp, hp)

    def stat(self, name: str) -> float:
        return self.stats[name] * boost_multiplier(self.mon.boosts.get(name, 0))


def stab(attacker: Pokemon, move: Move) -> float:
    own = move.type in attacker.base_types
    tera = attacker.is_terastallized and move.type == attacker.tera_type
    if not (own or tera):
        return 1.0
    adaptability = attacker.ability == "adaptability"
    if tera and own:
        return 2.25 if adaptability else 2.0
    return 2.0 if adaptability else 1.5


def damage(attacker: Fighter, defender: Fighter, move: Move, battle: Battle,
           defender_side: Dict[SideCondition, int]) -> float:
    """Mean damage in HP points (see the module docstring)."""
    if move.category == MoveCategory.STATUS:
        return 0.0
    if move.entry.get("damage") == "level":
        return float(attacker.mon.level or 100)
    if move.id in HALF_HP_MOVES:
        return math.floor(defender.hp / 2)
    power = move.base_power
    if not power:
        return 0.0
    physical = move.category == MoveCategory.PHYSICAL
    offense = move.entry.get("overrideOffensiveStat") or ("atk" if physical else "spa")
    source = defender if move.entry.get("overrideOffensivePokemon") == "target" else attacker
    a = source.stat(offense)
    d = defender.stat(move.entry.get("overrideDefensiveStat") or ("def" if physical else "spd"))
    level = attacker.mon.level or 100
    base = math.floor(math.floor(math.floor(2 * level / 5 + 2) * power * a / max(d, 1.0)) / 50) + 2
    mult = stab(attacker.mon, move) * defender.mon.damage_multiplier(move)
    if physical and attacker.mon.status == Status.BRN:
        mult *= 0.5
    weather = set(battle.weather)
    if weather & set(SUN):
        mult *= {PokemonType.FIRE: 1.5, PokemonType.WATER: 0.5}.get(move.type, 1.0)
    elif weather & set(RAIN):
        mult *= {PokemonType.WATER: 1.5, PokemonType.FIRE: 0.5}.get(move.type, 1.0)
    screen = SideCondition.REFLECT if physical else SideCondition.LIGHT_SCREEN
    if screen in defender_side or SideCondition.AURORA_VEIL in defender_side:
        mult *= 0.5
    if move.entry.get("willCrit"):
        mult *= 1.5
    lo, hi = move.n_hit
    hits = 3.1 if (lo, hi) == (2, 5) else (lo + hi) / 2
    return base * mult * MEAN_ROLL * hits


def expected_fraction(attacker: Fighter, defender: Fighter, move: Optional[Move],
                      battle: Battle, defender_side) -> tuple:
    """-> (accuracy * min(damage, current HP) / HP, KO possible)."""
    if move is None or defender.max_hp <= 0:
        return 0.0, False
    dmg = damage(attacker, defender, move, battle, defender_side)
    return (float(move.accuracy) * min(dmg, defender.hp) / defender.max_hp,
            defender.hp > 0 and dmg >= defender.hp)


def speed(f: Fighter, side: Dict[SideCondition, int]) -> float:
    s = f.stat("spe")
    if f.mon.status == Status.PAR:
        s *= 0.5
    if f.mon.item == "choicescarf":
        s *= 1.5
    if SideCondition.TAILWIND in side:
        s *= 2.0
    return s


def move_order(ours: Fighter, theirs: Fighter, battle: Battle) -> int:
    """1 if ours moves first (same priority), -1 if theirs does, 0 on a speed tie."""
    a, b = speed(ours, battle.side_conditions), speed(theirs, battle.opponent_side_conditions)
    if a == b:
        return 0
    first = a > b
    if Field.TRICK_ROOM in battle.fields:
        first = not first
    return 1 if first else -1
