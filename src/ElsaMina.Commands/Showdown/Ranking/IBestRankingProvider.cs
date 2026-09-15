namespace ElsaMina.Commands.Showdown.Ranking;

public interface IBestRankingProvider
{
    /// <summary>
    /// Gets the user's highest Elo ranking with a displayable format name, or <c>null</c> when the user has none.
    /// </summary>
    Task<RankingDataDto> GetBestRankingAsync(string userId, CancellationToken cancellationToken = default);
}
