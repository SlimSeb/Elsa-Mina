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

    public static TarotScoreResult Compute(TarotScoreInput input)
    {
        var takerHalfPoints = input.TakerHalfPoints;
        var petitAuBoutSide = input.PetitAuBoutSide;
        var poigneeHalfPoints = input.PoigneeHalfPoints;

        var target = GetTargetHalfPoints(input.OudlerCount);
        var diff = takerHalfPoints - target;
        var made = diff >= 0;
        var madeSign = made ? 1 : -1;
        var baseHalfPoints = BASE_HALF_POINTS + Math.Abs(diff);
        var multiplier = TarotConstants.BID_MULTIPLIER[input.Bid];
        var contractValue = baseHalfPoints * multiplier;

        // The petit au bout follows the contract multiplier; the poignée and chelem bonuses are flat.
        var petitAuBoutHalfPoints = petitAuBoutSide * TarotConstants.PETIT_AU_BOUT_HALF_POINTS * multiplier;
        var poigneeContribution = madeSign * poigneeHalfPoints;
        var slamHalfPoints = ComputeSlamHalfPoints(input.SlamWinnerSide, input.SlamAnnounced);

        var perDefender = madeSign * contractValue
                          + petitAuBoutHalfPoints
                          + poigneeContribution
                          + slamHalfPoints;

        var deltas = Distribute(perDefender, input.PlayerCount, input.TakerIndex, input.PartnerIndex);
        ApplyMiserePayments(deltas, input.MiserePlayerHalfPoints);

        return new TarotScoreResult
        {
            OudlerCount = input.OudlerCount,
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
            SlamWinnerSide = input.SlamWinnerSide,
            SlamAnnounced = input.SlamAnnounced,
            SlamHalfPoints = slamHalfPoints,
            PerDefenderHalfPoints = perDefender,
            Deltas = deltas
        };
    }

    /// <summary>
    /// Overlays the personal misère primes onto the deal deltas. Each player receives their own misère
    /// bonus from every other player and pays every other player's, keeping the total zero-sum.
    /// </summary>
    private static void ApplyMiserePayments(int[] deltas, IReadOnlyList<int> miserePlayerHalfPoints)
    {
        if (miserePlayerHalfPoints is null)
        {
            return;
        }

        var playerCount = deltas.Length;
        var total = 0;
        for (var i = 0; i < playerCount; i++)
        {
            total += miserePlayerHalfPoints[i];
        }

        if (total == 0)
        {
            return;
        }

        for (var i = 0; i < playerCount; i++)
        {
            deltas[i] += miserePlayerHalfPoints[i] * playerCount - total;
        }
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
