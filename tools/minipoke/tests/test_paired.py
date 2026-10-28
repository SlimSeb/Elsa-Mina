"""paired.py: team pairs, their order on both sides, and pair-by-pair scores."""

import shutil
from pathlib import Path
from types import SimpleNamespace

import pytest

from paired import (BASE_FORMAT, PAIRED_FORMAT, TeamPairs, battle_score, generate_teams, make_paired,
                    pair_scores, paired_player, team_names)
from rollout import SafeRandomPlayer

SHOWDOWN = Path.home() / "Documents/minipoke/pokemon-showdown"


def team(*names):
    return "]".join(f"{n}||Leftovers|Pressure|tackle||85,85,85,85,85,85||||80|" for n in names)


PAIRS = TeamPairs([team("A1", "A2"), team("B1", "B2")], [team("C1", "C2"), team("D1", "D2")])


def battle(names, won=None, finished=True):
    return SimpleNamespace(team={f"p1: {n}": None for n in names}, finished=finished,
                           won=won is True, lost=won is False)


def test_each_side_gets_the_pairs_in_challenge_order_swapped_on_the_second_game():
    challenger, accepter = PAIRS.challenger(), PAIRS.accepter()
    a = [team_names(challenger.yield_team()) for _ in range(4)]
    b = [team_names(accepter.yield_team()) for _ in range(4)]
    assert a == [frozenset({"A1", "A2"}), frozenset({"C1", "C2"}), frozenset({"B1", "B2"}),
                 frozenset({"D1", "D2"})]
    assert b == [a[1], a[0], a[3], a[2]]
    assert PAIRS.locate(frozenset({"D1", "D2"})) == (1, 1)


def test_pair_scores_average_each_pair_and_take_the_interval_from_their_spread():
    battles = [battle(["A1", "A2"], won=True), battle(["C1", "C2"], won=True),    # pair 0: 1
               battle(["B1", "B2"], won=True), battle(["D1", "D2"], won=False),   # pair 1: 0.5
               battle(["X1"], won=True), battle(["A1", "A2"], finished=False)]
    result = pair_scores(battles, PAIRS)
    assert (result.pairs, result.games, result.misplaced, result.ties) == (2, 5, 1, 0)
    assert result.score == pytest.approx(0.75)
    assert result.low < 0.75 < result.high


def test_a_tie_scores_half():
    assert battle_score(battle([], won=None)) == 0.5
    assert battle_score(battle([], won=False)) == 0.0


def test_paired_players_challenge_with_the_rules_and_create_battles_of_the_base_format():
    player = SafeRandomPlayer(start_listening=False)
    make_paired(player, PAIRS.challenger())
    assert player._format == PAIRED_FORMAT and player.format == BASE_FORMAT == "gen9anythinggoes"
    assert "!Team Preview" in PAIRED_FORMAT and type(player) is paired_player(SafeRandomPlayer)
    assert team_names(player.next_team) == frozenset({"A1", "A2"})


@pytest.mark.skipif(shutil.which("node") is None or not (SHOWDOWN / "dist").exists(),
                    reason="needs Node and the Showdown clone")
def test_generated_teams_are_random_battle_teams_and_the_same_for_a_seed():
    teams = generate_teams(3, seed=5, showdown_cwd=SHOWDOWN)
    assert teams == generate_teams(3, seed=5, showdown_cwd=SHOWDOWN)
    assert teams != generate_teams(3, seed=6, showdown_cwd=SHOWDOWN)
    assert all(len(t.split("]")) == 6 for t in teams)


def test_pooled_pair_scores_give_the_result_of_one_run():
    from paired import summarize
    battles = [battle(["A1", "A2"], won=True), battle(["C1", "C2"], won=False),
               battle(["B1", "B2"], won=True), battle(["D1", "D2"], won=True)]
    whole = pair_scores(battles, PAIRS)
    first = pair_scores(battles[:2], TeamPairs(PAIRS.first[:1], PAIRS.second[:1]))
    second = pair_scores(battles[2:], TeamPairs(PAIRS.first[1:], PAIRS.second[1:]))
    pooled = summarize(first.scores + second.scores, first.games + second.games)
    assert (pooled.score, pooled.low, pooled.high, pooled.pairs) == (whole.score, whole.low, whole.high, 2)


def test_a_match_result_without_pairs_counts_ties_as_half():
    from rollout import MatchResult
    result = MatchResult(score=10.5, games=20, ties=1)
    assert result.rate == pytest.approx(0.525)
    assert "52.5% over 20 battles" in result.describe() and "1 ties" in result.describe()
