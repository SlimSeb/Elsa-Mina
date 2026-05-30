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

    public static TarotScoreResult Compute(int takerHalfPoints, int oudlerCount, TarotBid bid,
        int playerCount, int takerIndex, int partnerIndex)
    {
        var target = GetTargetHalfPoints(oudlerCount);
        var diff = takerHalfPoints - target;
        var made = diff >= 0;
        var baseHalfPoints = BASE_HALF_POINTS + Math.Abs(diff);
        var multiplier = TarotConstants.BID_MULTIPLIER[bid];
        var contractValue = baseHalfPoints * multiplier;

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
            Deltas = Distribute(contractValue, made, playerCount, takerIndex, partnerIndex)
        };
    }

    /// <summary>
    /// Zero-sum distribution of the contract value. Each defender wins/loses one unit; the taker
    /// side wins/loses as many units as there are defenders, split as 1 unit for the partner (when
    /// there is one) and the remainder for the taker.
    /// </summary>
    public static int[] Distribute(int contractValueHalfPoints, bool made, int playerCount,
        int takerIndex, int partnerIndex)
    {
        var deltas = new int[playerCount];
        var hasPartner = partnerIndex >= 0 && partnerIndex != takerIndex;
        var defenderCount = playerCount - 1 - (hasPartner ? 1 : 0);
        var takerUnits = defenderCount - (hasPartner ? 1 : 0);
        var sign = made ? 1 : -1;

        for (var i = 0; i < playerCount; i++)
        {
            if (i == takerIndex)
            {
                deltas[i] = sign * takerUnits * contractValueHalfPoints;
            }
            else if (hasPartner && i == partnerIndex)
            {
                deltas[i] = sign * contractValueHalfPoints;
            }
            else
            {
                deltas[i] = -sign * contractValueHalfPoints;
            }
        }

        return deltas;
    }
}
