namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Pure scoring logic for a Belote deal. Card points are passed in raw (before the last-trick bonus);
/// this adds the dix de der, resolves the contract, applies capot and belote bonuses, and distributes
/// the final round score to each team.
/// </summary>
public static class BeloteScorer
{
    public static BeloteScoreResult Compute(int takerTeam, int team0CardPoints, int team1CardPoints,
        int lastTrickTeam, int team0Tricks, int team1Tricks, int beloteTeam, IReadOnlyList<BelotePlayer> players)
    {
        // Bank the dix de der to the team that won the last trick.
        var team0Total = team0CardPoints + (lastTrickTeam == 0 ? BeloteConstants.LAST_TRICK_BONUS : 0);
        var team1Total = team1CardPoints + (lastTrickTeam == 1 ? BeloteConstants.LAST_TRICK_BONUS : 0);

        var takerCardPoints = takerTeam == 0 ? team0Total : team1Total;
        var defenderCardPoints = takerTeam == 0 ? team1Total : team0Total;

        var capotTeam = team0Tricks == BeloteConstants.TRICK_COUNT ? 0
            : team1Tricks == BeloteConstants.TRICK_COUNT ? 1
            : -1;

        int takerScore;
        int defenderScore;
        bool made;

        if (capotTeam == takerTeam)
        {
            made = true;
            takerScore = BeloteConstants.CAPOT_SCORE;
            defenderScore = 0;
        }
        else if (capotTeam != -1)
        {
            // The defenders swept the deal: the taker goes down, defenders bank the capot.
            made = false;
            takerScore = 0;
            defenderScore = BeloteConstants.CAPOT_SCORE;
        }
        else if (takerCardPoints > defenderCardPoints)
        {
            made = true;
            takerScore = takerCardPoints;
            defenderScore = defenderCardPoints;
        }
        else
        {
            // Failed or tied ("litige"): the defenders take every point.
            made = false;
            takerScore = 0;
            defenderScore = team0Total + team1Total;
        }

        var team0Score = takerTeam == 0 ? takerScore : defenderScore;
        var team1Score = takerTeam == 0 ? defenderScore : takerScore;

        // The belote-rebelote bonus is always scored, even by a side that goes down.
        if (beloteTeam == 0)
        {
            team0Score += BeloteConstants.BELOTE_BONUS;
        }
        else if (beloteTeam == 1)
        {
            team1Score += BeloteConstants.BELOTE_BONUS;
        }

        var deltas = new int[players.Count];
        for (var i = 0; i < players.Count; i++)
        {
            deltas[i] = players[i].Team == 0 ? team0Score : team1Score;
        }

        return new BeloteScoreResult
        {
            TakerTeam = takerTeam,
            Team0CardPoints = team0Total,
            Team1CardPoints = team1Total,
            LastTrickTeam = lastTrickTeam,
            BeloteTeam = beloteTeam,
            Team0Score = team0Score,
            Team1Score = team1Score,
            Made = made,
            IsCapot = capotTeam != -1,
            Deltas = deltas
        };
    }
}
