"""Offline poke-env battles: a request for our side, then protocol lines."""

import logging
from typing import List, Optional, Sequence

from poke_env.battle import Battle
from poke_env.data import to_id_str

LOG = logging.getLogger("minipoke-tests")
STATS = {"atk": 200, "def": 180, "spa": 150, "spd": 170, "spe": 190}


def mon(species: str, moves: Sequence[str], level: int = 80, item: str = "leftovers",
        ability: str = "pressure", tera: str = "Normal", hp: str = "250/250",
        active: bool = False, stats: Optional[dict] = None, role: str = "p1") -> dict:
    return {"ident": f"{role}: {species}", "details": f"{species}, L{level}",
            "condition": hp, "active": active, "stats": dict(stats or STATS),
            "moves": [to_id_str(m) for m in moves], "baseAbility": to_id_str(ability),
            "item": to_id_str(item), "pokeball": "pokeball", "ability": to_id_str(ability),
            "teraType": tera, "terastallized": ""}


def request(team: List[dict], can_tera: bool = True, force_switch: bool = False,
            trapped: bool = False, disabled: Sequence[str] = ()) -> dict:
    active = next(m for m in team if m["active"])
    req = {"side": {"name": "me", "id": team[0]["ident"][:2], "pokemon": team}, "rqid": 1}
    if force_switch:
        req["forceSwitch"] = [True]
    else:
        req["active"] = [{
            "moves": [{"move": m, "id": m, "pp": 8, "maxpp": 8, "target": "normal",
                       "disabled": m in disabled} for m in active["moves"]],
            "trapped": trapped,
            **({"canTerastallize": active["teraType"]} if can_tera else {}),
        }]
    return req


def make_battle(team: List[dict], lines: Sequence[str] = (), tag: str = "battle-gen9randombattle-1",
                **request_kwargs) -> Battle:
    """Our team from a request, then the protocol lines (opponent switches, moves...)."""
    battle = Battle(tag, "me", LOG, gen=9)
    role = team[0]["ident"][:2]
    battle._player_role = role
    for m in team:
        if m["active"]:
            feed(battle, f"|switch|{role}a: {m['ident'][4:]}|{m['details']}|{m['condition']}")
    battle.parse_request(request(team, **request_kwargs))
    feed(battle, *lines)
    return battle


def feed(battle: Battle, *lines: str) -> None:
    for line in lines:
        battle.parse_message(line.split("|"))


def default_team(active: int = 0) -> List[dict]:
    team = [
        mon("Garchomp", ["Earthquake", "Outrage", "Stealth Rock", "Swords Dance"], level=77,
            ability="roughskin", tera="Ground"),
        mon("Rotom-Wash", ["Hydro Pump", "Volt Switch", "Will-O-Wisp", "Pain Split"], level=86,
            ability="levitate", tera="Electric"),
        mon("Corviknight", ["Brave Bird", "Roost", "Defog", "U-turn"], level=81,
            ability="mirrorarmor", tera="Dragon"),
    ]
    team[active]["active"] = True
    return team


def opponent_switch(species: str, level: int = 84, hp: str = "100/100", role: str = "p2") -> str:
    return f"|switch|{role}a: {species}|{species}, L{level}|{hp}"
