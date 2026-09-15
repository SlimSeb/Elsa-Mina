namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Everything <see cref="BeloteScorer.Compute"/> needs to score a deal: the raw card points each team
/// captured (before the last-trick bonus), who won the last trick and how many tricks each side took,
/// which team holds belote-rebelote, and the seats the resulting deltas are laid out over.
/// </summary>
public sealed record BeloteScoreInput
{
    /// <summary>
    /// Team (0 or 1) of the player who took the contract.
    /// </summary>
    public required int TakerTeam { get; init; }

    /// <summary>
    /// Card points captured by each team, before the dix de der is banked.
    /// </summary>
    public required int Team0CardPoints { get; init; }

    public required int Team1CardPoints { get; init; }

    /// <summary>
    /// Team that won the last trick, and so banks the dix de der.
    /// </summary>
    public required int LastTrickTeam { get; init; }

    /// <summary>
    /// Number of tricks won by each team, used to detect a capot.
    /// </summary>
    public required int Team0Tricks { get; init; }

    public required int Team1Tricks { get; init; }

    /// <summary>
    /// Team holding belote-rebelote (King and Queen of trump), or -1 if neither team does.
    /// </summary>
    public required int BeloteTeam { get; init; }

    /// <summary>
    /// The seats of the deal, in the order the returned deltas are indexed by.
    /// </summary>
    public required IReadOnlyList<BelotePlayer> Players { get; init; }
}
