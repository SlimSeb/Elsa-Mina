"""
Whether a move can work on its target: minipoke-v2's `move_effects` features
(encoder_v2.py, ModelConfig.move_effects).

V1's move features (encoder.py) read poke-env's type chart only
(Pokemon.damage_multiplier), which knows nothing of abilities or items: Earthquake
into a Levitate Pokémon looks super effective, and nothing says it had no effect the
last time. For each move (owner = the Pokémon that has it, target = the opposing
active, V1's convention), the features are:

    0  no effect          1 when the type chart (damaging moves, and the status moves
                          that follow it), an item rule or a target type rule makes it
                          a sure miss, else P(the target's ability blocks it)
    1  ability blocks     P(the target's ability blocks it): the target's ability, else
                          its set priors' ability weights (opponents), else poke-env's
                          possible abilities, uniformly; times P(the owner's ability is
                          not a Mold Breaker); 0 for moves that ignore abilities and
                          under Neutralizing Gas
    2  status fails       a status move inflicting a major status into a target that
                          already has one
    3  immune before      a move of the same name from the owner's side had no effect
                          on this target (|-immune|), after its last |-terastallize|
    4  failed last time   the owner's last use of the move failed (|-fail|)
    5  failed twice       its last two uses failed

3-5 read the protocol log (history.move_outcomes).

A rule matches a move when every condition it has holds (lists: any of them):
    types        the move's type (poke-env Move.type, lower case)
    categories   its category (physical, special, status)
    flags        one of its data flags (sound, bullet, powder, wind, reflectable...)
    statuses     the major status it inflicts (its data "status")
    moves        its id
    except       not one of these ids
    targets      its data target (default FOE_TARGETS)
    target_types the target has one of these types (poke-env Pokemon.types)
    min_priority its data priority is at least this
    not_super_effective  damaging and the type chart multiplier is below 2
    unless_owner the owner's ability is none of these (a revealed ability only)
"""

from __future__ import annotations

from typing import Dict, Iterable, List, Optional, Sequence, Tuple

from poke_env.battle import Field, Move, Pokemon

from encoder import _GD

FOE_TARGETS = ["normal", "any", "adjacentFoe", "allAdjacent", "allAdjacentFoes", "randomNormal"]
DAMAGING = ["physical", "special"]

# Showdown's gen 9 abilities that make the holder immune to a move (onTryHit,
# onImmunity, onFoeTryMove). Every one of them is breakable (Mold Breaker...).
ABILITY_RULES: Dict[str, dict] = {
    "levitate": {"types": ["ground"], "categories": DAMAGING, "except": ["thousandarrows"]},
    "eartheater": {"types": ["ground"]},
    "voltabsorb": {"types": ["electric"]},
    "lightningrod": {"types": ["electric"]},
    "motordrive": {"types": ["electric"]},
    "waterabsorb": {"types": ["water"]},
    "stormdrain": {"types": ["water"]},
    "dryskin": {"types": ["water"]},
    "flashfire": {"types": ["fire"]},
    "wellbakedbody": {"types": ["fire"]},
    "sapsipper": {"types": ["grass"]},
    "bulletproof": {"flags": ["bullet"]},
    "soundproof": {"flags": ["sound"]},
    "overcoat": {"flags": ["powder"]},
    "windrider": {"flags": ["wind"]},
    "goodasgold": {"categories": ["status"]},
    "magicbounce": {"categories": ["status"], "flags": ["reflectable"],
                    "targets": FOE_TARGETS + ["foeSide"]},
    "wonderguard": {"categories": DAMAGING, "not_super_effective": True},
    "armortail": {"min_priority": 1},
    "dazzling": {"min_priority": 1},
    "queenlymajesty": {"min_priority": 1},
    "insomnia": {"categories": ["status"], "statuses": ["slp"]},
    "vitalspirit": {"categories": ["status"], "statuses": ["slp"]},
    "sweetveil": {"categories": ["status"], "statuses": ["slp"]},
    "limber": {"categories": ["status"], "statuses": ["par"]},
    "waterveil": {"categories": ["status"], "statuses": ["brn"]},
    "waterbubble": {"categories": ["status"], "statuses": ["brn"]},
    "thermalexchange": {"categories": ["status"], "statuses": ["brn"]},
    "immunity": {"categories": ["status"], "statuses": ["psn", "tox"]},
    "pastelveil": {"categories": ["status"], "statuses": ["psn", "tox"]},
    "comatose": {"categories": ["status"], "statuses": ["brn", "par", "psn", "tox", "slp", "frz"]},
    "purifyingsalt": {"categories": ["status"], "statuses": ["brn", "par", "psn", "tox", "slp", "frz"]},
}
# A revealed item of the target (poke-env Pokemon.item)
ITEM_RULES: Dict[str, dict] = {
    "airballoon": {"types": ["ground"], "categories": DAMAGING, "except": ["thousandarrows"]},
    "safetygoggles": {"flags": ["powder"]},
}
# The target's types (status immunities, powder, Leech Seed)
TYPE_RULES: List[dict] = [
    {"categories": ["status"], "statuses": ["brn"], "target_types": ["fire"], "except": ["toxicthread"]},
    {"categories": ["status"], "statuses": ["par"], "target_types": ["electric"]},
    {"categories": ["status"], "statuses": ["psn", "tox"], "target_types": ["poison", "steel"],
     "except": ["toxicthread"], "unless_owner": ["corrosion"]},
    {"flags": ["powder"], "target_types": ["grass"]},
    {"moves": ["leechseed"], "target_types": ["grass"]},
]
BREAKERS = ["moldbreaker", "teravolt", "turboblaze"]
# Status moves that follow the type chart (Showdown "ignoreImmunity": false): Thunder Wave
TYPE_CHART_STATUS_MOVES = ["thunderwave"]
# Status moves whose only effect is a major status: they fail into a statused target
STATUS_FAIL_EXCEPT = ["toxicthread"]
F_EFFECT = 6
FEATURES = ["no effect", "ability blocks", "status fails", "immune before",
            "failed last time", "failed the last two times"]

Weights = List[Tuple[str, float]]


# Moves that ignore the target's ability (Showdown "ignoreAbility")
IGNORE_ABILITY_MOVES = frozenset(k for k, v in _GD.moves.items() if v.get("ignoreAbility"))


def type_name(t) -> str:
    return t.name.lower()


def move_type(move: Move) -> str:
    try:
        return type_name(move.type)
    except Exception:
        return ""


def rule_matches(rule: dict, move: Move, target: Pokemon, owner_ability: Optional[str],
                 multiplier: float) -> bool:
    entry = move.entry
    category = str(entry.get("category", "")).lower()
    if "types" in rule and move_type(move) not in rule["types"]:
        return False
    if "categories" in rule and category not in rule["categories"]:
        return False
    if "flags" in rule and not any(f in (entry.get("flags") or {}) for f in rule["flags"]):
        return False
    if "statuses" in rule and entry.get("status") not in rule["statuses"]:
        return False
    if "moves" in rule and move.id not in rule["moves"]:
        return False
    if move.id in rule.get("except", ()):
        return False
    if entry.get("target") not in rule.get("targets", FOE_TARGETS):
        return False
    if "target_types" in rule and not any(type_name(t) in rule["target_types"] for t in target.types):
        return False
    if "min_priority" in rule and entry.get("priority", 0) < rule["min_priority"]:
        return False
    if rule.get("not_super_effective") and (category not in DAMAGING or multiplier >= 2):
        return False
    if owner_ability is not None and owner_ability in rule.get("unless_owner", ()):
        return False
    return True


def ability_weights(mon: Optional[Pokemon], prior_abilities: Optional[Weights] = None) -> Weights:
    """The Pokémon's ability, else its priors' weights, else its possible abilities,
    uniformly (in poke-env's order)."""
    if mon is None:
        return []
    if mon.ability is not None:
        return [(mon.ability, 1.0)]
    if prior_abilities:
        return list(prior_abilities)
    possible = mon.possible_abilities or []
    return [(a, 1.0 / len(possible)) for a in possible]


def weight_of(weights: Iterable[Tuple[str, float]], names: Sequence[str]) -> float:
    total = 0.0
    for name, w in weights:
        if name in names:
            total += w
    return total


def static_features(move: Move, owner: Pokemon, target: Optional[Pokemon], owner_abilities: Weights,
                    target_abilities: Weights, neutralizing_gas: bool) -> Tuple[float, float, float]:
    """(no effect, ability blocks, status fails) of `owner`'s `move` on `target`."""
    if target is None:
        return 0.0, 0.0, 0.0
    entry = move.entry
    category = str(entry.get("category", "")).lower()
    try:
        multiplier = target.damage_multiplier(move)
    except Exception:
        multiplier = 1.0
    owner_ability = owner.ability
    sure = multiplier == 0 and (category in DAMAGING or move.id in TYPE_CHART_STATUS_MOVES)
    item_rule = ITEM_RULES.get(target.item or "")
    sure = sure or (item_rule is not None and rule_matches(item_rule, move, target, owner_ability,
                                                           multiplier))
    sure = sure or any(rule_matches(r, move, target, owner_ability, multiplier) for r in TYPE_RULES)
    blocks = 0.0
    if not neutralizing_gas and move.id not in IGNORE_ABILITY_MOVES:
        blocking = [a for a, _ in target_abilities
                    if a in ABILITY_RULES and rule_matches(ABILITY_RULES[a], move, target,
                                                           owner_ability, multiplier)]
        blocks = weight_of(target_abilities, blocking) * (1.0 - weight_of(owner_abilities, BREAKERS))
    fails = (category == "status" and entry.get("status") is not None
             and move.id not in STATUS_FAIL_EXCEPT and entry.get("target") in FOE_TARGETS
             and target.status is not None)
    return (1.0 if sure else blocks), blocks, float(fails)


def neutralizing_gas(battle) -> bool:
    return Field.NEUTRALIZING_GAS in battle.fields


def spec() -> dict:
    """encoder_spec.json's "move_effects" section: every rule, as data."""
    return {
        "features": FEATURES,
        "owner_target": "owner = the Pokémon with the move (candidates: that opponent), target = "
                        "the opposing active (V1's move features); no target: 0, 0, 0 for 0-2",
        "foe_targets": FOE_TARGETS,
        "damaging": DAMAGING,
        "ability_rules": ABILITY_RULES,
        "item_rules": ITEM_RULES,
        "type_rules": TYPE_RULES,
        "breakers": BREAKERS,
        "ignore_ability_moves": sorted(IGNORE_ABILITY_MOVES),
        "type_chart_status_moves": TYPE_CHART_STATUS_MOVES,
        "status_fail_except": STATUS_FAIL_EXCEPT,
        "rule_fields": "a rule matches when every field it has holds: types (Move.type, lower "
                       "case), categories, flags (any data flag), statuses (data status), moves, "
                       "except, targets (data target, default foe_targets), target_types (any of "
                       "Pokemon.types), min_priority (data priority >=), not_super_effective "
                       "(damaging and multiplier < 2), unless_owner (owner's known ability)",
        "no_effect": "1 when a damaging move (or a type_chart_status_moves one) has "
                     "multiplier 0 (Pokemon.damage_multiplier, 1.0 if it raises), when the target's "
                     "known item has a matching item rule, or a type rule matches; else ability "
                     "blocks",
        "ability_blocks": "sum of the target's ability weights whose ability rule matches, times "
                          "(1 - sum of the owner's breaker weights); 0 for ignore_ability_moves and "
                          "under Neutralizing Gas (Field.NEUTRALIZING_GAS). Weights: Pokemon.ability "
                          "(1), else the opponent's set prior abilities in their order, else "
                          "possible_abilities, 1/n each, in their order; summed in that order",
        "status_fails": "category status, a data status, not in status_fail_except, target in "
                        "foe_targets and the target's Pokemon.status is not None",
        "history": "history.move_outcomes: chosen |move| lines (no [from] tag), Struggle "
                   "skipped; a |-fail| line after one (before the next move, switch, drag, "
                   "replace, turn, cant or upkeep line) marks it failed, an |-immune| line on the "
                   "other side marks (user side, move id, immune side, name) at that line; "
                   "immune before = that line comes after the target's last |-terastallize| "
                   "line; failed last time / twice = the last one / two uses of (owner side, "
                   "owner name, move id) failed. Names are the ident after ': '",
    }
