"""
Random-battle set priors for encoder v2.

Showdown builds every gen9randombattle team from a JSON file of sets: per species,
a few roles ("Fast Attacker", "Bulky Support"...) each with a movepool, abilities
and tera types. Once an opponent's Pokémon has shown a move, an ability or its tera
type, only some of its species' roles are still possible; their pools say which
moves it probably also has.

    priors = SetPriors.from_showdown("pokemon-showdown")      # finds the sets file
    prior = priors.infer_mon(battle.opponent_active_pokemon)
    prior.candidates   # [(move id, fraction of consistent roles with it)], best first

Rules (encoder_spec.json documents them for the C# port):
  * Species: the Pokémon's species id, else its dex entry's `battleOnly` form(s),
    then `baseSpecies` (Palafin-Hero -> palafin, Toxtricity-Low-Key -> toxtricity).
  * Evidence: revealed moves, the revealed ability, the revealed item, and the tera
    type once terastallized. Evidence outside the species' whole pool says nothing
    about its roles (Struggle, moves called by Sleep Talk, Trace, Illusion...) and is
    ignored. A role is consistent when it contains every remaining piece of evidence.
    No consistent role -> every role of the species ("fallback").
  * Candidates: moves of the consistent roles not revealed yet, ranked by the
    fraction of consistent roles containing them (descending), ties by move id
    (ascending), the first `k` kept.
  * Abilities, items, tera types: weight of x = mean over consistent roles of
    [x in role] / |role's x| (a distribution). A revealed one gets weight 1.
  * Choice item possible: the item is a revealed Choice item, or it is not revealed,
    no revealed move is a status move other than Trick / Switcheroo / Healing Wish,
    and a consistent role can be dealt a set without status moves (at least
    min(4, pool size) damaging moves in its pool) or has one of those three moves.
    That mirrors gen 9 singles' getItem in Showdown's teams.ts, which hands out
    Choice items to all-attacking sets and to Trick / Switcheroo / Healing Wish users.

Showdown's gen 9 sets have no items (teams.ts picks them in code), so the item
prior is empty with them; older sets files have no abilities either.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

from poke_env.data import GenData, to_id_str

# Where Showdown kept the gen 9 random battle sets, newest first
SETS_FILES = [
    "data/random-battles/gen9/sets.json",   # since May 2024
    "data/random-sets.json",                # Dec 2022 - May 2024 (gen 9 was the current gen)
]
CHOICE_ITEMS = ("choiceband", "choicescarf", "choicespecs")
# A set with one of these gets a Choice item even with status moves (teams.ts getItem)
CHOICE_STATUS_MOVES = ("healingwish", "switcheroo", "trick")

_GD = GenData.from_gen(9)


def find_sets_file(path: Optional[str | Path] = None, showdown_dir: str | Path = "pokemon-showdown"
                   ) -> Path:
    """`path` if given (a sets file, or a Showdown clone), else the gen 9 sets file
    of the Showdown clone in `showdown_dir`."""
    roots = [Path(path)] if path else [Path(showdown_dir)]
    for root in roots:
        if root.is_file():
            return root
        for rel in SETS_FILES:
            if (root / rel).is_file():
                return root / rel
    raise FileNotFoundError(f"no gen 9 random battle sets in {roots[0]} (looked for "
                            f"{', '.join(SETS_FILES)}); pass --sets PATH")


def is_damaging(move_id: str) -> bool:
    entry = _GD.moves.get(move_id)
    return entry is not None and entry.get("category") != "Status"


@dataclass(frozen=True)
class Role:
    name: str
    moves: Tuple[str, ...]       # move ids, sorted
    abilities: Tuple[str, ...]   # ability ids, sorted (empty when the file has none)
    items: Tuple[str, ...]       # item ids, sorted (empty when the file has none)
    tera_types: Tuple[str, ...]  # type ids ("water"), sorted

    @property
    def choice_capable(self) -> bool:
        damaging = sum(1 for m in self.moves if is_damaging(m))
        return (damaging >= min(4, len(self.moves))
                or any(m in self.moves for m in CHOICE_STATUS_MOVES))


@dataclass
class MonPrior:
    """What the priors say about one opponent Pokémon."""
    found: bool = False                      # species has sets
    n_roles: int = 0
    n_consistent: int = 0
    fallback: bool = False                   # nothing consistent: all roles used
    candidates: List[Tuple[str, float]] = field(default_factory=list)
    abilities: List[Tuple[str, float]] = field(default_factory=list)
    items: List[Tuple[str, float]] = field(default_factory=list)
    tera_types: Dict[str, float] = field(default_factory=dict)
    choice_possible: bool = False


def _weights(roles: Sequence[Role], attr: str, revealed: Optional[str]) -> List[Tuple[str, float]]:
    if revealed is not None:
        return [(revealed, 1.0)]
    acc: Dict[str, float] = {}
    for r in roles:
        values = getattr(r, attr)
        for v in values:
            acc[v] = acc.get(v, 0.0) + 1.0 / len(values) / len(roles)
    return sorted(acc.items(), key=lambda kv: (-kv[1], kv[0]))


class SetPriors:
    def __init__(self, species: Dict[str, List[Role]], source_sha256: str = "",
                 source: str = ""):
        self.species = species
        self.source_sha256 = source_sha256   # sha256 of the Showdown file's bytes
        self.source = source
        self._pools: Dict[str, Tuple[frozenset, frozenset, frozenset, frozenset]] = {}

    # Shared, never mutated: model copies (frozen_copy) keep pointing at the same one
    def __deepcopy__(self, memo):
        return self

    # ------------------------------------------------------------------ loading
    @classmethod
    def from_sets_file(cls, path: str | Path) -> "SetPriors":
        priors = cls.from_sets_file_bytes(Path(path).read_bytes())
        priors.source = str(path)
        return priors

    @classmethod
    def from_sets_file_bytes(cls, raw: bytes) -> "SetPriors":
        data = json.loads(raw)
        species: Dict[str, List[Role]] = {}
        for key, entry in data.items():
            roles = []
            for s in entry.get("sets", []):
                roles.append(Role(
                    name=s.get("role", ""),
                    moves=tuple(sorted({to_id_str(m) for m in s.get("movepool", [])})),
                    abilities=tuple(sorted({to_id_str(a) for a in s.get("abilities", [])})),
                    items=tuple(sorted({to_id_str(i) for i in s.get("items", [])})),
                    tera_types=tuple(sorted({to_id_str(t) for t in s.get("teraTypes", [])})),
                ))
            if roles:
                species[to_id_str(key)] = roles
        return cls(species, hashlib.sha256(raw).hexdigest())

    @classmethod
    def from_showdown(cls, path: Optional[str | Path] = None,
                      showdown_dir: str | Path = "pokemon-showdown") -> "SetPriors":
        return cls.from_sets_file(find_sets_file(path, showdown_dir))

    def to_dict(self) -> dict:
        """Normalized form (what set_priors.json holds, and checkpoints)."""
        return {
            "source_sha256": self.source_sha256,
            "species": {k: [{"role": r.name, "moves": list(r.moves),
                             "abilities": list(r.abilities), "items": list(r.items),
                             "tera_types": list(r.tera_types)} for r in roles]
                        for k, roles in sorted(self.species.items())},
        }

    @classmethod
    def from_dict(cls, d: dict) -> "SetPriors":
        species = {k: [Role(r["role"], tuple(r["moves"]), tuple(r["abilities"]),
                            tuple(r["items"]), tuple(r["tera_types"])) for r in roles]
                   for k, roles in d["species"].items()}
        return cls(species, d.get("source_sha256", ""), d.get("source", ""))

    def to_json(self) -> str:
        return json.dumps(self.to_dict(), indent=1, sort_keys=True)

    @property
    def sha256(self) -> str:
        """Hash of the normalized priors (set_priors.json's bytes)."""
        return hashlib.sha256(self.to_json().encode()).hexdigest()

    # ------------------------------------------------------------------ lookup
    def key_for(self, species: str) -> Optional[str]:
        sid = to_id_str(species)
        if sid in self.species:
            return sid
        entry = _GD.pokedex.get(sid, {})
        battle_only = entry.get("battleOnly")
        names = battle_only if isinstance(battle_only, list) else [battle_only]
        for name in names + [entry.get("baseSpecies")]:
            if name and to_id_str(name) in self.species:
                return to_id_str(name)
        return None

    def _pool(self, key: str):
        if key not in self._pools:
            roles = self.species[key]
            self._pools[key] = tuple(frozenset(x for r in roles for x in getattr(r, a))
                                     for a in ("moves", "abilities", "items", "tera_types"))
        return self._pools[key]

    # ------------------------------------------------------------------ inference
    def infer(self, species: str, revealed_moves: Iterable[str] = (),
              ability: Optional[str] = None, item: Optional[str] = None,
              tera_type: Optional[str] = None, k: int = 8) -> MonPrior:
        """`item`: the revealed item id, "" once it is known to hold none (knocked off,
        consumed), None while unknown. `tera_type`: the type id once terastallized."""
        key = self.key_for(species)
        if key is None:
            return MonPrior()
        roles = self.species[key]
        move_pool, ability_pool, item_pool, tera_pool = self._pool(key)
        revealed = [to_id_str(m) for m in revealed_moves]
        moves = [m for m in revealed if m in move_pool]
        ability = to_id_str(ability) if ability else None
        ability = ability if ability in ability_pool else None
        item = to_id_str(item) if item else item
        item_ev = item if item and item in item_pool else None
        tera = to_id_str(tera_type) if tera_type else None
        tera = tera if tera in tera_pool else None

        consistent = [r for r in roles
                      if all(m in r.moves for m in moves)
                      and (ability is None or ability in r.abilities)
                      and (item_ev is None or item_ev in r.items)
                      and (tera is None or tera in r.tera_types)]
        fallback = not consistent
        used = roles if fallback else consistent

        counts: Dict[str, int] = {}
        for r in used:
            for m in r.moves:
                counts[m] = counts.get(m, 0) + 1
        known = set(revealed)
        ranked = sorted(((m, c / len(used)) for m, c in counts.items() if m not in known),
                        key=lambda mc: (-mc[1], mc[0]))

        if item in CHOICE_ITEMS:
            choice = True
        elif item is not None:  # another item, or none left
            choice = False
        else:
            status = [m for m in revealed if not is_damaging(m) and m not in CHOICE_STATUS_MOVES]
            choice = not status and any(r.choice_capable for r in used)

        return MonPrior(
            found=True, n_roles=len(roles), n_consistent=len(consistent), fallback=fallback,
            candidates=ranked[:k],
            abilities=_weights(used, "abilities", ability),
            items=_weights(used, "items", item_ev) if item_pool else [],
            tera_types=dict(_weights(used, "tera_types", tera)),
            choice_possible=choice,
        )

    def infer_mon(self, mon, k: int = 8) -> MonPrior:
        """infer() from a poke-env Pokemon of the opponent's team. Its base ability:
        an ability gained in battle (Trace, Skill Swap) is not part of its set."""
        item = None if mon.item == GenData.UNKNOWN_ITEM else (mon.item or "")
        tera = mon.tera_type.name.lower() if mon.is_terastallized and mon.tera_type else None
        return self.infer(mon.species, mon.moves.keys(), mon.base_ability, item, tera, k)
