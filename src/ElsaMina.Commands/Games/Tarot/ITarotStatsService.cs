namespace ElsaMina.Commands.Games.Tarot;

public interface ITarotStatsService
{
    /// <summary>
    /// Persists the outcome of a finished deal: updates each player's cumulative score and counters.
    /// </summary>
    Task RecordDealAsync(IReadOnlyList<TarotPlayer> players, TarotScoreResult result,
        CancellationToken cancellationToken = default);
}
