namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Outcome of scoring a Belote deal. All values are whole points.
/// </summary>
public sealed record BeloteScoreResult
{
    public int TakerTeam { get; init; }

    /// <summary>
    /// Raw card points captured by each team, including the last-trick bonus ("dix de der").
    /// </summary>
    public int Team0CardPoints { get; init; }
    public int Team1CardPoints { get; init; }

    /// <summary>
    /// Team that won the last trick, and so banked the dix de der.
    /// </summary>
    public int LastTrickTeam { get; init; }

    /// <summary>
    /// Team holding belote-rebelote (King and Queen of trump), or -1 if neither team does.
    /// </summary>
    public int BeloteTeam { get; init; }

    /// <summary>
    /// Final round scores awarded to each team, including capot, contract and belote bonuses.
    /// </summary>
    public int Team0Score { get; init; }
    public int Team1Score { get; init; }

    /// <summary>
    /// True when the taker side reached or exceeded the points needed for their contract.
    /// </summary>
    public bool Made { get; init; }

    /// <summary>
    /// True when one team won every trick.
    /// </summary>
    public bool IsCapot { get; init; }

    /// <summary>
    /// Round score per player, indexed like the player list. Both players of a team share their team score.
    /// </summary>
    public int[] Deltas { get; init; } = [];
}
