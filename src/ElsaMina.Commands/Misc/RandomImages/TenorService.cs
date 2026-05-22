using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.RandomImages;

public class TenorService : ITenorService
{
    private const string TENOR_SEARCH_URL = "https://g.tenor.com/v2/search";
    private const int RESULT_LIMIT = 10;

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;
    private readonly IRandomService _randomService;

    public TenorService(IHttpService httpService, IConfiguration configuration, IRandomService randomService)
    {
        _httpService = httpService;
        _configuration = configuration;
        _randomService = randomService;
    }

    public async Task<TenorMediaInfo> GetRandomMediaAsync(string query, string mediaFormat,
        CancellationToken cancellationToken = default)
    {
        var results = await FetchResultsAsync(query, cancellationToken);
        if (results == null || results.Count == 0)
        {
            return null;
        }

        var selected = results[_randomService.NextInt(results.Count)];
        return ExtractMediaInfo(selected, mediaFormat);
    }

    public async Task<List<TenorMediaInfo>> GetMultipleMediaAsync(string query, string mediaFormat, int count,
        CancellationToken cancellationToken = default)
    {
        var results = await FetchResultsAsync(query, cancellationToken);
        if (results == null || results.Count == 0)
        {
            return [];
        }

        var shuffled = results.OrderBy(_ => _randomService.NextInt(results.Count)).Take(count);
        return shuffled
            .Select(result => ExtractMediaInfo(result, mediaFormat))
            .Where(info => info != null)
            .ToList();
    }

    private async Task<List<TenorResultDto>> FetchResultsAsync(string query,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration.TenorApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Tenor API key is empty.");
            return null;
        }

        var queryParams = new Dictionary<string, string>
        {
            ["q"] = query,
            ["key"] = apiKey,
            ["limit"] = RESULT_LIMIT.ToString(),
            ["media_filter"] = "minimal"
        };

        try
        {
            var response = await _httpService.GetAsync<TenorResponseDto>(TENOR_SEARCH_URL, queryParams,
                cancellationToken: cancellationToken);
            return response.Data?.Results;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch Tenor media for query: {Query}", query);
            return null;
        }
    }

    private static TenorMediaInfo ExtractMediaInfo(TenorResultDto result, string mediaFormat)
    {
        if (result.MediaFormats == null || !result.MediaFormats.TryGetValue(mediaFormat, out var media))
        {
            return null;
        }

        return new TenorMediaInfo(media.Url, media.Dims[0], media.Dims[1]);
    }
}