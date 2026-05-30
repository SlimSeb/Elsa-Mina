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
    /// Net score change per player, indexed like the player list, in half-points. Sums to zero.
    /// </summary>
    public int[] Deltas { get; init; } = [];
}
