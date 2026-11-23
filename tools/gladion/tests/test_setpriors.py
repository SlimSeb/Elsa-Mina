import json

import pytest

from setpriors import SetPriors, find_sets_file
from tests.battles import default_team, make_battle, opponent_switch
from tests.priors import SETS, hand_priors


@pytest.fixture(scope="module")
def priors():
    return hand_priors()


def test_revealing_a_move_excludes_the_roles_without_it(priors):
    nothing = priors.infer("venusaur")
    assert (nothing.n_roles, nothing.n_consistent, nothing.fallback) == (3, 3, False)
    p = priors.infer("venusaur", ["earthpower"])
    assert p.n_consistent == 2                       # Bulky Support has no Earth Power
    assert "leechseed" not in dict(p.candidates)     # only Bulky Support has it
    assert "earthpower" not in dict(p.candidates)    # revealed: not a candidate
    p = priors.infer("venusaur", ["earthpower", "teraBlast"])
    assert p.n_consistent == 1
    assert dict(p.candidates) == {"gigadrain": 1.0, "growth": 1.0}
    assert p.abilities == [("chlorophyll", 1.0)] and p.tera_types == {"fire": 1.0}


def test_ability_and_tera_evidence(priors):
    p = priors.infer("venusaur", ability="overgrow")
    assert p.n_consistent == 2 and p.abilities == [("overgrow", 1.0)]
    p = priors.infer("venusaur", tera_type="dark")
    assert p.n_consistent == 1 and p.tera_types == {"dark": 1.0}
    assert dict(p.candidates)["earthpower"] == 1.0


def test_no_consistent_role_falls_back_to_the_whole_species_pool(priors):
    # Leech Seed (support only) + Tera Blast (Tera Blast user only)
    p = priors.infer("venusaur", ["leechseed", "terablast"], k=99)
    assert (p.n_consistent, p.fallback) == (0, True)
    assert dict(p.candidates)["sludgebomb"] == pytest.approx(2 / 3)
    assert {m for m, _ in priors.infer("venusaur", k=99).candidates} == {
        m for m, _ in p.candidates} | {"leechseed", "terablast"}


def test_evidence_outside_the_species_pool_is_ignored(priors):
    # Struggle, a move called by Sleep Talk, a Traced ability...
    assert priors.infer("venusaur", ["struggle", "earthquake"], ability="intimidate").n_consistent == 3


def test_candidate_ranking_is_deterministic(priors):
    p = priors.infer("venusaur", k=8)
    # 2/3: earthpower, gigadrain, sleeppowder, sludgebomb (ties by id), then 1/3 by id
    assert p.candidates == [
        ("earthpower", pytest.approx(2 / 3)), ("gigadrain", pytest.approx(2 / 3)),
        ("sleeppowder", pytest.approx(2 / 3)), ("sludgebomb", pytest.approx(2 / 3)),
        ("energyball", pytest.approx(1 / 3)), ("growth", pytest.approx(1 / 3)),
        ("knockoff", pytest.approx(1 / 3)), ("leechseed", pytest.approx(1 / 3))]
    assert priors.infer("venusaur", k=3).candidates == p.candidates[:3]
    shuffled = SetPriors.from_dict(json.loads(json.dumps(priors.to_dict())))
    assert shuffled.infer("venusaur", k=8).candidates == p.candidates


def test_ability_and_tera_weights(priors):
    p = priors.infer("venusaur")
    # chlorophyll: 1/2 + 1/2 + 1 over 3 roles; overgrow: 1/2 + 1/2
    assert p.abilities == [("chlorophyll", pytest.approx(2 / 3)), ("overgrow", pytest.approx(1 / 3))]
    assert sum(p.tera_types.values()) == pytest.approx(1.0)
    assert p.tera_types["fire"] == pytest.approx(1 / 3)
    assert p.items == []                             # gen 9 sets have no items


def test_choice_item_possible(priors):
    assert priors.infer("garchomp").choice_possible           # all-attacking pool
    assert priors.infer("garchomp", item="choicescarf").choice_possible
    assert not priors.infer("garchomp", item="lifeorb").choice_possible
    assert not priors.infer("garchomp", item="").choice_possible   # knocked off
    assert not priors.infer("venusaur", ["sleeppowder"]).choice_possible  # status move shown
    # Bulky Attacker can be dealt 4 attacks; the Tera Blast user has 3 in a 4-move pool
    assert priors.infer("venusaur", ["earthpower"]).choice_possible
    assert not priors.infer("venusaur", ["earthpower", "terablast"]).choice_possible


def test_species_forms_use_their_base_sets(priors):
    assert priors.key_for("palafinhero") == "palafin"          # battleOnly
    assert priors.key_for("garchomp") == "garchomp"
    assert priors.key_for("pikachu") is None
    assert not priors.infer("pikachu", ["thunderbolt"]).found


def test_priors_round_trip_and_hash(priors):
    again = SetPriors.from_dict(json.loads(priors.to_json()))
    assert again.sha256 == priors.sha256 and again.to_json() == priors.to_json()
    assert again.source_sha256 == priors.source_sha256 != ""


def test_finds_the_sets_file_where_showdown_kept_it(tmp_path):
    new = tmp_path / "new" / "data/random-battles/gen9/sets.json"
    old = tmp_path / "old" / "data/random-sets.json"
    for f in (new, old):
        f.parent.mkdir(parents=True)
        f.write_text(json.dumps(SETS))
    assert find_sets_file(None, tmp_path / "new") == new
    assert find_sets_file(None, tmp_path / "old") == old
    assert find_sets_file(old) == old
    with pytest.raises(FileNotFoundError):
        find_sets_file(None, tmp_path / "nothing")


def test_infer_from_a_poke_env_battle(priors):
    battle = make_battle(default_team(), [opponent_switch("Venusaur"), "|turn|1",
                                          "|move|p2a: Venusaur|Earth Power|p1a: Garchomp",
                                          "|turn|2"])
    p = priors.infer_mon(battle.opponent_active_pokemon)
    assert p.n_consistent == 2 and "earthpower" not in dict(p.candidates)
