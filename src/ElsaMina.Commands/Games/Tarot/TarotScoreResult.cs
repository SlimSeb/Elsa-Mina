namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Outcome of scoring a tarot deal. All point fields are in half-points (real value × 2).
/// </summary>
public sealed record TarotScoreResult
{
    public int OudlerCount { get; init; }
    public int TargetHalfPoints { get; init; }
    public int TakerHalfPoints { get; init; }
    public int DiffHalfPoints { get; init; }
    public bool Made { get; init; }
    public int Multiplier { get; init; }
    public int BaseHalfPoints { get; init; }
    public int ContractValueHalfPoints { get; init; }

    /// <summary>
    /// +1 if the taker side won the Petit in the last trick, -1 for the defenders, 0 otherwise.
    /// </summary>
    public int PetitAuBoutSide { get; init; }

    /// <summary>
    /// The petit au bout bonus, in half-points, from the taker side's point of view (already multiplied).
    /// </summary>
    public int PetitAuBoutHalfPoints { get; init; }

    /// <summary>
    /// Total declared poignée bonus, in half-points (unsigned). Always benefits the side that wins the deal.
    /// </summary>
    public int PoigneeHalfPoints { get; init; }

    /// <summary>
    /// +1 if the taker side slammed, -1 if the defenders did, 0 if there was no slam.
    /// </summary>
    public int SlamWinnerSide { get; init; }

    public bool SlamAnnounced { get; init; }

    /// <summary>
    /// The chelem bonus, in half-points, from the taker side's point of view.
    /// </summary>
    public int SlamHalfPoints { get; init; }

    /// <summary>
    /// The signed per-defender amount, in half-points, that the distribution is built from.
    /// </summary>
    public int PerDefenderHalfPoints { get; init; }

    /// <summary>
    /// Net score change per player, indexed like the player list, in half-points. Sums to zero.
    /// </summary>
    public int[] Deltas { get; init; } = [];
}
