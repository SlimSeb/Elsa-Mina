import random

import pytest

from elo import ANCHORS, expected, fit_ratings, parse_anchors, round_robin_pairs


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
