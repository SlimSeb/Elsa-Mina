namespace ElsaMina.Battles.Results;

/// <summary>
/// The results of the bot's battles since it started, by format and strategy.
/// </summary>
public interface IBattleResultsService
{
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
