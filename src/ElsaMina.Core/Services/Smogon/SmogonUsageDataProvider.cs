using ElsaMina.Core.Services.Http;

namespace ElsaMina.Core.Services.Smogon;

public class SmogonUsageDataProvider : ISmogonUsageDataProvider
{
    private const string USAGE_DATA_URL = "https://www.smogon.com/stats/{0}/chaos/{1}-{2}.json";

    // Smogon buckets its chaos files by glicko2 rating cutoff. Most formats publish 0/1500/1630/1760,
    // but the highest-traffic ladders get a wider spread and publish 0/1500/1695/1825 instead.
    private static readonly HashSet<string> WideRatingFormats = ["gen9ou", "gen9doublesou"];

    private readonly IHttpService _httpService;

    public SmogonUsageDataProvider(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public async Task<SmogonUsageDataDto> GetUsageDataAsync(string month, string format, Level playerLevel,
        CancellationToken cancellationToken = default)
    {
        var ratingCutoff = GetRatingCutoff(format, playerLevel);
        var url = string.Format(USAGE_DATA_URL, month, format, ratingCutoff);
        var response = await _httpService.SendAsync<SmogonUsageDataDto>(HttpRequest.Get(url), cancellationToken);
        return response.Data;
    }

    private static int GetRatingCutoff(string format, Level playerLevel)
    {
        var isWideRatingFormat = WideRatingFormats.Contains(format);
        return playerLevel switch
        {
            Level.Low => 0,
            Level.Mid => 1500,
            Level.High => isWideRatingFormat ? 1695 : 1630,
            Level.VeryHigh => isWideRatingFormat ? 1825 : 1760,
            _ => 0
        };
    }
}
