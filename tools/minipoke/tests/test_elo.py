import json
import random

import pytest

from elo import (ANCHORS, LEARNER, EloTable, expected, fit_ratings, parse_anchors,
                 round_robin_pairs, snapshot_name)


def test_anchors_stay_fixed_and_the_learner_moves_toward_its_performance():
    table = EloTable(k=16)
    table.add_player(LEARNER, table.rating("heuristic"))
    assert table.rating(LEARNER) == ANCHORS["heuristic"]
    table.record(LEARNER, "heuristic", score_a=150, games=200)    # 75%: ~ +190 Elo
    assert table.rating("heuristic") == ANCHORS["heuristic"]
    gained = table.rating(LEARNER) - ANCHORS["heuristic"]
    assert 150 < gained < 200                                    # converges, no overshoot
    perf = ANCHORS["heuristic"] + 400 * __import__("math").log10(3)
    for _ in range(20):
        table.record(LEARNER, "heuristic", 150, 200)
    assert table.rating(LEARNER) == pytest.approx(perf, abs=1.0)
    assert table.games[LEARNER] == 21 * 200 and table.games["heuristic"] == 21 * 200


def test_learner_and_snapshot_exchange_points():
    table = EloTable(k=16)
    table.add_player(LEARNER, 800.0)
    table.add_player(snapshot_name(10))      # starts at the learner's rating
    assert table.rating("it00010") == 800.0
    table.record(LEARNER, "it00010", 15, 20)
    up = table.rating(LEARNER) - 800.0
    assert up > 0 and table.rating("it00010") == pytest.approx(800.0 - up)
    table.record("it00010", LEARNER, 5, 20)  # either order: one result row
    assert table.results == {"it00010|learner": [10.0, 40]}
    table.record(LEARNER, LEARNER, 10, 20)   # mirror games carry nothing
    assert table.games[LEARNER] == 40


def test_table_round_trips_and_saves(tmp_path):
    table = EloTable(k=16)
    table.add_player(LEARNER, 700.0)
    table.record(LEARNER, "maxpower", 18, 20)
    other = EloTable(k=16)
    other.load_state_dict(json.loads(json.dumps(table.state_dict())))
    assert other.ratings == table.ratings and other.results == table.results
    table.save(tmp_path / "elo.json")
    saved = json.loads((tmp_path / "elo.json").read_text())
    assert saved["table"][0]["player"] in ("heuristic", LEARNER)
    assert {r["player"] for r in saved["table"]} == {LEARNER, "heuristic", "maxpower", "random"}


def test_fit_recovers_ratings_with_anchors_fixed():
    true = {"random": 0.0, "maxpower": 410.0, "a": 650.0, "b": 900.0}
    rng = random.Random(0)
    results = {}
    for x, y in round_robin_pairs(true):
        n = 4000
        wins = sum(rng.random() < expected(true[x], true[y]) for _ in range(n))
        results[(x, y)] = (wins, n)
    fit = fit_ratings(results, {"random": 0.0, "maxpower": 410.0})
    assert fit["random"] == 0.0 and fit["maxpower"] == pytest.approx(410.0)
    assert fit["a"] == pytest.approx(650.0, abs=25) and fit["b"] == pytest.approx(900.0, abs=35)
    centred = fit_ratings({("a", "b"): (1000, 2000)}, {})
    assert centred["a"] == pytest.approx(0.0, abs=1e-6) and centred["b"] == pytest.approx(0.0, abs=1e-6)
    # a player who never won keeps a finite rating
    assert fit_ratings({("a", "random"): (0, 50)}, {"random": 0.0})["a"] < -300


def test_parse_anchors():
    assert parse_anchors(None) == ANCHORS
    assert parse_anchors("random=0, heuristic=750") == {"random": 0.0, "heuristic": 750.0}
