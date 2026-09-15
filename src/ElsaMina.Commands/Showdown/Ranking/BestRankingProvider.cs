using ElsaMina.Core.Services.Formats;

namespace ElsaMina.Commands.Showdown.Ranking;

public class BestRankingProvider : IBestRankingProvider
{
    private readonly IShowdownRanksProvider _showdownRanksProvider;
    private readonly IFormatsManager _formatsManager;

    public BestRankingProvider(IShowdownRanksProvider showdownRanksProvider, IFormatsManager formatsManager)
    {
        _showdownRanksProvider = showdownRanksProvider;
        _formatsManager = formatsManager;
    }

    public async Task<RankingDataDto> GetBestRankingAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rankings = await _showdownRanksProvider.GetRankingDataAsync(userId, cancellationToken);
        var bestRanking = rankings?.MaxBy(ranking => ranking.Elo);
        bestRanking?.FormatId = _formatsManager.GetCleanFormat(bestRanking.FormatId);
        return bestRanking;
    }
}
