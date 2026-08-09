using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipyService : IKlipyService
{
    private const string KLIPY_API_BASE_URL = "https://api.klipy.com/api/v1";

    /// <summary>
    /// KLIPY rejects a per_page outside of these bounds.
    /// </summary>
    private const int MIN_PER_PAGE = 8;

    private const int MAX_PER_PAGE = 50;

    /// <summary>
    /// How many hits to pull before picking one at random, so the random commands stay varied.
    /// </summary>
    private const int RANDOM_POOL_SIZE = 24;

    /// <summary>
    /// Showdown rooms are all-ages, so the strictest safety filter is used.
    /// </summary>
    private const string CONTENT_FILTER = "high";

    /// <summary>
    /// Shown in the search mosaic. Eight of these load at once, so the lightest tier is used
    /// (~70 KB and 90px wide, against ~300 KB for sm).
    /// </summary>
    private const KlipyMediaSize PREVIEW_SIZE = KlipyMediaSize.Xs;

    /// <summary>
    /// Posted in chat once a search hit is picked. sm is the largest tier that stays chat-friendly:
    /// md and hd gifs routinely run several MB, and their md width is often below sm's anyway.
    /// </summary>
    private const KlipyMediaSize FULL_SIZE = KlipyMediaSize.Sm;

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;
    private readonly IRandomService _randomService;

    public KlipyService(IHttpService httpService, IConfiguration configuration, IRandomService randomService)
    {
        _httpService = httpService;
        _configuration = configuration;
        _randomService = randomService;
    }

    public async Task<GifMediaInfo> GetRandomMediaAsync(string query, KlipyMediaSize size, KlipyMediaFormat format,
        CancellationToken cancellationToken = default)
    {
        var items = await FetchItemsAsync(query, RANDOM_POOL_SIZE, cancellationToken);
        if (items == null)
        {
            return null;
        }

        var candidates = items
            .Select(item => ExtractMediaInfo(item, size, format))
            .Where(mediaInfo => mediaInfo != null)
            .ToList();

        return candidates.Count == 0
            ? null
            : candidates[_randomService.NextInt(candidates.Count)];
    }

    public async Task<List<GifSearchResult>> SearchAsync(string query, int count,
        CancellationToken cancellationToken = default)
    {
        var items = await FetchItemsAsync(query, count, cancellationToken);
        if (items == null)
        {
            return [];
        }

        // KLIPY asks that search results keep the order and composition it returned them in.
        return items
            .Select(item => new
            {
                Preview = ExtractMediaInfo(item, PREVIEW_SIZE, KlipyMediaFormat.Gif),
                Full = ExtractMediaInfo(item, FULL_SIZE, KlipyMediaFormat.Gif)
            })
            .Where(pair => pair.Preview != null && pair.Full != null)
            .Take(count)
            .Select(pair => new GifSearchResult(pair.Preview, pair.Full))
            .ToList();
    }

    private async Task<List<KlipyItem>> FetchItemsAsync(string query, int requestedCount,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration.KlipyApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Klipy API key is empty.");
            return null;
        }

        var perPage = Math.Clamp(requestedCount, MIN_PER_PAGE, MAX_PER_PAGE);
        var queryParams = new Dictionary<string, string>
        {
            ["q"] = query,
            ["per_page"] = perPage.ToString(),
            ["page"] = "1",
            ["content_filter"] = CONTENT_FILTER,
            ["format_filter"] = "gif,mp4"
        };

        var url = $"{KLIPY_API_BASE_URL}/{Uri.EscapeDataString(apiKey)}/gifs/search";

        try
        {
            var response = await _httpService.SendAsync<KlipySearchResponse>(
                HttpRequest.Get(url).WithQueryParameters(queryParams), cancellationToken);
            return response.Data?.Data?.Items;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to fetch Klipy media for query: {Query}", query);
            return null;
        }
    }

    private static GifMediaInfo ExtractMediaInfo(KlipyItem item, KlipyMediaSize size, KlipyMediaFormat format)
    {
        if (item.File == null
            || !item.File.TryGetValue(ToWireKey(size), out var formats)
            || formats == null
            || !formats.TryGetValue(ToWireKey(format), out var file)
            || string.IsNullOrWhiteSpace(file?.Url))
        {
            return null;
        }

        return new GifMediaInfo(file.Url, file.Width, file.Height);
    }

    private static string ToWireKey(KlipyMediaSize size) => size.ToString().ToLowerInvariant();

    private static string ToWireKey(KlipyMediaFormat format) => format.ToString().ToLowerInvariant();
}
