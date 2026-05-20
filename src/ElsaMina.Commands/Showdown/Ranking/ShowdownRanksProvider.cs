using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Showdown.Ranking;

public class ShowdownRanksProvider : IShowdownRanksProvider
{
    private const string RANK_RESOURCE_URL =
        "https://play.pokemonshowdown.com/~~showdown/action.php?act=ladderget&user={0}";

    private const int MAX_ATTEMPTS = 3;
    private static readonly TimeSpan DEFAULT_RETRY_DELAY = TimeSpan.FromSeconds(1);

    private readonly IHttpService _httpService;
    private readonly TimeSpan _retryDelay;

    public ShowdownRanksProvider(IHttpService httpService)
        : this(httpService, DEFAULT_RETRY_DELAY)
    {
    }

    public ShowdownRanksProvider(IHttpService httpService, TimeSpan retryDelay)
    {
        _httpService = httpService;
        _retryDelay = retryDelay;
    }

    public async Task<IEnumerable<RankingDataDto>> GetRankingDataAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        Exception lastException = null;

        for (var attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(_retryDelay, cancellationToken);
            }

            try
            {
                var result = await _httpService.GetAsync<IEnumerable<RankingDataDto>>(
                    string.Format(RANK_RESOURCE_URL, userId),
                    removeFirstCharacterFromResponse: true,
                    cancellationToken: cancellationToken);
                return result.Data;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Log.Error(ex, "GetRankingDataAsync attempt {Attempt}/{Max} failed for user {UserId}",
                    attempt + 1, MAX_ATTEMPTS, userId);
            }
        }

        throw lastException!;
    }
}