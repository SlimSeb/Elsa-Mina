namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Pure scoring logic for a French Tarot deal. Everything is computed in half-points
/// (real value × 2) so that the half-point card values stay exact integers.
/// </summary>
public static class TarotScorer
{
    private const int BASE_HALF_POINTS = 50; // 25 points, doubled

    public static int GetTargetHalfPoints(int oudlerCount) =>
        TarotConstants.TARGET_HALF_POINTS[Math.Clamp(oudlerCount, 0, 3)];

    /// <param name="petitAuBoutSide">+1 if the taker side won the Petit in the last trick, -1 if the
    /// defenders did, 0 if the Petit was not in the last trick.</param>
    /// <param name="poigneeHalfPoints">Total declared poignée bonus, in half-points. Always benefits
    /// the side that wins the deal.</param>
    /// <param name="slamWinnerSide">+1 if the taker side won every trick, -1 if the defenders did,
    /// 0 if there was no slam.</param>
    /// <param name="slamAnnounced">Whether the taker announced a chelem before play.</param>
    public static TarotScoreResult Compute(int takerHalfPoints, int oudlerCount, TarotBid bid,
        int playerCount, int takerIndex, int partnerIndex,
        int petitAuBoutSide = 0, int poigneeHalfPoints = 0,
        int slamWinnerSide = 0, bool slamAnnounced = false)
    {
        var target = GetTargetHalfPoints(oudlerCount);
        var diff = takerHalfPoints - target;
        var made = diff >= 0;
        var madeSign = made ? 1 : -1;
        var baseHalfPoints = BASE_HALF_POINTS + Math.Abs(diff);
        var multiplier = TarotConstants.BID_MULTIPLIER[bid];
        var contractValue = baseHalfPoints * multiplier;

        // The petit au bout follows the contract multiplier; the poignée and chelem bonuses are flat.
        var petitAuBoutHalfPoints = petitAuBoutSide * TarotConstants.PETIT_AU_BOUT_HALF_POINTS * multiplier;
        var poigneeContribution = madeSign * poigneeHalfPoints;
        var slamHalfPoints = ComputeSlamHalfPoints(slamWinnerSide, slamAnnounced);

        var perDefender = madeSign * contractValue
                          + petitAuBoutHalfPoints
                          + poigneeContribution
                          + slamHalfPoints;

        return new TarotScoreResult
        {
            OudlerCount = oudlerCount,
            TargetHalfPoints = target,
            TakerHalfPoints = takerHalfPoints,
            DiffHalfPoints = diff,
            Made = made,
            Multiplier = multiplier,
            BaseHalfPoints = baseHalfPoints,
            ContractValueHalfPoints = contractValue,
            PetitAuBoutSide = petitAuBoutSide,
            PetitAuBoutHalfPoints = petitAuBoutHalfPoints,
            PoigneeHalfPoints = poigneeHalfPoints,
            SlamWinnerSide = slamWinnerSide,
            SlamAnnounced = slamAnnounced,
            SlamHalfPoints = slamHalfPoints,
            PerDefenderHalfPoints = perDefender,
            Deltas = Distribute(perDefender, playerCount, takerIndex, partnerIndex)
        };
    }

    /// <summary>
    /// The chelem bonus, in half-points, from the taker side's point of view.
    /// </summary>
    private static int ComputeSlamHalfPoints(int slamWinnerSide, bool announced)
    {
        if (slamWinnerSide > 0)
        {
            return announced ? TarotConstants.SLAM_ANNOUNCED_HALF_POINTS : TarotConstants.SLAM_UNANNOUNCED_HALF_POINTS;
        }

        if (slamWinnerSide < 0)
        {
            // The defenders pulled off a slam: the bonus goes to them.
            return -TarotConstants.SLAM_UNANNOUNCED_HALF_POINTS;
        }

        // Nobody slammed: an unfulfilled slam announcement penalises the taker side.
        return announced ? -TarotConstants.SLAM_FAILED_HALF_POINTS : 0;
    }

    /// <summary>
    /// Zero-sum distribution of a (signed) per-defender amount. Each defender wins/loses one unit; the
    /// taker side wins/loses as many units as there are defenders, split as 1 unit for the partner (when
    /// there is one) and the remainder for the taker.
    /// </summary>
    public static int[] Distribute(int perDefenderHalfPoints, int playerCount, int takerIndex, int partnerIndex)
    {
        var deltas = new int[playerCount];
        var hasPartner = partnerIndex >= 0 && partnerIndex != takerIndex;
        var defenderCount = playerCount - 1 - (hasPartner ? 1 : 0);
        var takerUnits = defenderCount - (hasPartner ? 1 : 0);

        for (var i = 0; i < playerCount; i++)
        {
            if (i == takerIndex)
            {
                deltas[i] = takerUnits * perDefenderHalfPoints;
            }
            else if (hasPartner && i == partnerIndex)
            {
                deltas[i] = perDefenderHalfPoints;
            }
            else
            {
                deltas[i] = -perDefenderHalfPoints;
            }
        }

        return deltas;
    }
}
