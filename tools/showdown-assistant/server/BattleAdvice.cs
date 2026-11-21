namespace ElsaMina.BattleAssistant;

/// <summary>
/// What the server says after applying a room's lines.
/// </summary>
public sealed record BattleAdvice
{
    public required string RoomId { get; init; }
    public required AdviceStatus Status { get; init; }

    /// <summary>Why there is no advice (<see cref="AdviceStatus.Unsupported"/>).</summary>
    public string? Message { get; init; }

    /// <summary>The request the advice answers: the search endpoint takes it back.</summary>
    public int Rqid { get; init; }

    public int Turn { get; init; }
    public string Format { get; init; } = "";

    /// <summary>Whether the format is the one the model was trained on (gen9randombattle).</summary>
    public bool IsTrainedFormat { get; init; }

    /// <summary>
    /// The server did not see the battle from its start (it restarted mid-battle): the extension sends the room's
    /// whole log again.
    /// </summary>
    public bool Partial { get; init; }

    /// <summary>Whether the search endpoint can look one turn ahead (Showdown's simulator is configured).</summary>
    public bool CanSearch { get; init; }

    /// <summary>The model's value of the position: about -1 (lost) to +1 (won).</summary>
    public double? Value { get; init; }

    /// <summary>The action the model recommends: the most likely one the guard allows.</summary>
    public int? Best { get; init; }

    /// <summary>Every legal choice, most likely first.</summary>
    public IReadOnlyList<ActionAdvice> Actions { get; init; } = [];
}
