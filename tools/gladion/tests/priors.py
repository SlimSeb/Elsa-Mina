"""A hand-built SetPriors (no Showdown clone needed)."""

import json

from setpriors import SetPriors

SETS = {
    # 3 roles: "Bulky Support" and "Bulky Attacker" share Sludge Bomb, only the
    # attackers have Earth Power, only "Tera Blast user" has Tera Blast.
    "venusaur": {"level": 84, "sets": [
        {"role": "Bulky Support", "movepool": ["Giga Drain", "Leech Seed", "Sleep Powder",
                                                "Sludge Bomb", "Substitute"],
         "abilities": ["Chlorophyll", "Overgrow"], "teraTypes": ["Steel", "Water"]},
        {"role": "Bulky Attacker", "movepool": ["Earth Power", "Energy Ball", "Knock Off",
                                                 "Sleep Powder", "Sludge Bomb", "Synthesis"],
         "abilities": ["Chlorophyll", "Overgrow"], "teraTypes": ["Dark", "Steel", "Water"]},
        {"role": "Tera Blast user", "movepool": ["Earth Power", "Growth", "Tera Blast", "Giga Drain"],
         "abilities": ["Chlorophyll"], "teraTypes": ["Fire"]},
    ]},
    "garchomp": {"level": 77, "sets": [
        {"role": "Fast Attacker", "movepool": ["Earthquake", "Outrage", "Fire Fang", "Stone Edge",
                                                "Scale Shot"],
         "abilities": ["Rough Skin"], "teraTypes": ["Ground", "Steel"]},
    ]},
    "palafin": {"level": 76, "sets": [
        {"role": "Bulky Setup", "movepool": ["Bulk Up", "Drain Punch", "Jet Punch", "Wave Crash"],
         "abilities": ["Zero to Hero"], "teraTypes": ["Water"]},
    ]},
    # Flash Fire weight (1/2 + 1) / 2 = 0.75, Flame Body 0.25
    "heatran": {"level": 80, "sets": [
        {"role": "Bulky Support", "movepool": ["Magma Storm", "Earth Power", "Stealth Rock", "Taunt"],
         "abilities": ["Flash Fire", "Flame Body"], "teraTypes": ["Grass"]},
        {"role": "Wallbreaker", "movepool": ["Magma Storm", "Earth Power", "Flash Cannon"],
         "abilities": ["Flash Fire"], "teraTypes": ["Steel"]},
    ]},
}


def hand_priors() -> SetPriors:
    return SetPriors.from_sets_file_bytes(json.dumps(SETS).encode())
