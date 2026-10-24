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
}


def hand_priors() -> SetPriors:
    return SetPriors.from_sets_file_bytes(json.dumps(SETS).encode())
