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

        var capotTeam = DetermineCapotTeam(team0Tricks, team1Tricks);
        var (takerScore, defenderScore, made) =
            ResolveContract(takerTeam, capotTeam, takerCardPoints, defenderCardPoints, team0Total + team1Total);

        var team0Score = takerTeam == 0 ? takerScore : defenderScore;
        var team1Score = takerTeam == 0 ? defenderScore : takerScore;

        (team0Score, team1Score) = ApplyBeloteBonus(beloteTeam, team0Score, team1Score);

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

    /// <summary>
    /// The team (0 or 1) that won every trick, or -1 when neither side made a capot.
    /// </summary>
    private static int DetermineCapotTeam(int team0Tricks, int team1Tricks)
    {
        if (team0Tricks == BeloteConstants.TRICK_COUNT)
        {
            return 0;
        }

        if (team1Tricks == BeloteConstants.TRICK_COUNT)
        {
            return 1;
        }

        return -1;
    }

    /// <summary>
    /// Resolves the contract into the taker/defender card scores and whether the taker made it.
    /// </summary>
    private static (int TakerScore, int DefenderScore, bool Made) ResolveContract(int takerTeam, int capotTeam,
        int takerCardPoints, int defenderCardPoints, int totalCardPoints)
    {
        if (capotTeam == takerTeam)
        {
            return (BeloteConstants.CAPOT_SCORE, 0, true);
        }

        if (capotTeam != -1)
        {
            // The defenders swept the deal: the taker goes down, defenders bank the capot.
            return (0, BeloteConstants.CAPOT_SCORE, false);
        }

        if (takerCardPoints > defenderCardPoints)
        {
            return (takerCardPoints, defenderCardPoints, true);
        }

        // Failed or tied ("litige"): the defenders take every point.
        return (0, totalCardPoints, false);
    }

    /// <summary>
    /// Adds the belote-rebelote bonus, which is always scored, even by a side that goes down.
    /// </summary>
    private static (int Team0Score, int Team1Score) ApplyBeloteBonus(int beloteTeam, int team0Score, int team1Score)
    {
        if (beloteTeam == 0)
        {
            return (team0Score + BeloteConstants.BELOTE_BONUS, team1Score);
        }

        if (beloteTeam == 1)
        {
            return (team0Score, team1Score + BeloteConstants.BELOTE_BONUS);
        }

        return (team0Score, team1Score);
    }
}
