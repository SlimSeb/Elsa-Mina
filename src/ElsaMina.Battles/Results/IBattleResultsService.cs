namespace ElsaMina.Battles.Results;

/// <summary>
/// The results of the bot's battles since it started, by format and strategy.
/// </summary>
public interface IBattleResultsService
{
    /// <summary>
    /// How an ended battle went for the bot, null when the server did not name the winner.
    /// </summary>
    BattleOutcome? GetOutcome(BattleMessageResult result);

    /// <summary>
    /// Remembers which strategy plays the battle.
    /// </summary>
    void OnBattleStarted(BattleContext context);

    void OnBattleEnded(BattleContext context, BattleOutcome outcome);

    /// <summary>
    /// One record per format and strategy, most played first.
    /// </summary>
    IReadOnlyList<BattleRecord> GetRecords();

    /// <summary>
    /// The last battle that ended, null before the first one.
    /// </summary>
    BattleResult LastResult { get; }
}
