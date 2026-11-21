namespace ElsaMina.BattleAssistant;

/// <summary>
/// What the one-turn search chose for a request.
/// </summary>
/// <param name="Action">The action it found best (<see cref="ActionAdvice.Action"/>), null without a result.</param>
public sealed record SearchAdvice(string RoomId, int Rqid, SearchStatus Status, int? Action, string? Message = null);
