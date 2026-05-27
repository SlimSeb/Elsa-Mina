using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.System;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Showdown.Ranking;

public class ShowdownRanksProvider : IShowdownRanksProvider
{
    private const string RANK_RESOURCE_URL =
        "https://play.pokemonshowdown.com/~~showdown/action.php?act=ladderget&user={0}";

    private const int MAX_ATTEMPTS = 3;
    private static readonly TimeSpan DEFAULT_RETRY_DELAY = TimeSpan.FromSeconds(2);

    private readonly IHttpService _httpService;
    private readonly TimeSpan _retryDelay;
    private readonly ISystemService _systemService;

    public ShowdownRanksProvider(IHttpService httpService, ISystemService systemService)
        : this(httpService, systemService, DEFAULT_RETRY_DELAY)
    {
    }

    public ShowdownRanksProvider(IHttpService httpService, ISystemService systemService, TimeSpan retryDelay)
    {
        _httpService = httpService;
        _retryDelay = retryDelay;
        _systemService = systemService;
    }

    public async Task<IEnumerable<RankingDataDto>> GetRankingDataAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        Exception lastException = null;

        for (var attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
        {
            if (attempt > 0)
            {
                await _systemService.SleepAsync(_retryDelay, cancellationToken);
            }

            try
            {
                var result = await _httpService.SendAsync<IEnumerable<RankingDataDto>>(
                    HttpRequest.Get(string.Format(RANK_RESOURCE_URL, userId)).SkippingFirstResponseCharacter(),
                    cancellationToken);
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