using ElsaMina.Battles.Strategies;

namespace ElsaMina.Battles.Results;

/// <summary>
/// One of the bot's battles, once over.
/// </summary>
public sealed record BattleResult(
    string RoomId,
    string Format,
    BattleDecisionStrategy Strategy,
    BattleOutcome Outcome,
    int Turns,
    DateTimeOffset EndedAt);
