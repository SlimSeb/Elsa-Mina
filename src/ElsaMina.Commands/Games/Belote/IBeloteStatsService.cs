namespace ElsaMina.Commands.Games.Belote;

public interface IBeloteStatsService
{
    /// <summary>
    /// Persists the outcome of a finished deal: updates each player's cumulative score and counters.
    /// </summary>
    Task RecordDealAsync(IReadOnlyList<BelotePlayer> players, BeloteScoreResult result,
        CancellationToken cancellationToken = default);
}
