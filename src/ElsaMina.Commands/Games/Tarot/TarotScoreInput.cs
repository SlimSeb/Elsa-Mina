namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Everything <see cref="TarotScorer.Compute"/> needs to score a deal. All point values are in
/// half-points (real value × 2).
/// </summary>
public sealed record TarotScoreInput
{
    /// <summary>
    /// Card points captured by the taker side, in half-points.
    /// </summary>
    public required int TakerHalfPoints { get; init; }

    /// <summary>
    /// Number of oudlers in the taker side's pile, which sets the target to reach.
    /// </summary>
    public required int OudlerCount { get; init; }

    /// <summary>
    /// The contract that was taken, which sets the multiplier.
    /// </summary>
    public required TarotBid Bid { get; init; }

    public required int PlayerCount { get; init; }

    /// <summary>
    /// Seat index of the taker, and of their called partner, or -1 when the taker plays alone.
    /// </summary>
    public required int TakerIndex { get; init; }

    public required int PartnerIndex { get; init; }

    /// <summary>
    /// +1 if the taker side won the Petit in the last trick, -1 if the defenders did, 0 if the Petit
    /// was not in the last trick.
    /// </summary>
    public int PetitAuBoutSide { get; init; }

    /// <summary>
    /// Total declared poignée bonus, in half-points. Always benefits the side that wins the deal.
    /// </summary>
    public int PoigneeHalfPoints { get; init; }

    /// <summary>
    /// +1 if the taker side won every trick, -1 if the defenders did, 0 if there was no slam.
    /// </summary>
    public int SlamWinnerSide { get; init; }

    /// <summary>
    /// Whether the taker announced a chelem before play.
    /// </summary>
    public bool SlamAnnounced { get; init; }

    /// <summary>
    /// Per-player misère bonus, in half-points, indexed like the player list. Each amount is paid to
    /// that player by every other player, independent of the contract.
    /// </summary>
    public IReadOnlyList<int> MiserePlayerHalfPoints { get; init; }
}
