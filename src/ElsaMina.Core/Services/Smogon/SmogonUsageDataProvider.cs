using System.Globalization;
using ElsaMina.Core.Services.Http;

namespace ElsaMina.Core.Services.Smogon;

public class SmogonUsageDataProvider : ISmogonUsageDataProvider
{
    private const string USAGE_DATA_URL = "https://www.smogon.com/stats/{0}/chaos/{1}-{2}.json";
    private const string USAGE_RANKING_URL = "https://www.smogon.com/stats/{0}/{1}-{2}.txt";

    // | 1    | Great Tusk         | 34.36666% | 588614 | 27.084% | 462714 | 26.926% |
    private const int RANK_COLUMN = 1;
    private const int POKEMON_COLUMN = 2;
    private const int USAGE_COLUMN = 3;
    private const int RAW_COUNT_COLUMN = 4;
    private const int MINIMUM_COLUMN_COUNT = 5;

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

    public async Task<IReadOnlyList<SmogonUsageRankingEntryDto>> GetUsageRankingAsync(string month, string format,
        Level playerLevel, CancellationToken cancellationToken = default)
    {
        var ratingCutoff = GetRatingCutoff(format, playerLevel);
        var url = string.Format(USAGE_RANKING_URL, month, format, ratingCutoff);
        var response = await _httpService.SendForStringAsync(HttpRequest.Get(url), cancellationToken);
        return ParseUsageRanking(response.Data);
    }

    /// <summary>
    /// Parse le tableau ASCII publié par Smogon. Les lignes de séparation commencent par '+' et
    /// l'en-tête a "Rank" en première colonne : les deux sont ignorées faute de rang numérique.
    /// </summary>
    private static IReadOnlyList<SmogonUsageRankingEntryDto> ParseUsageRanking(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var entries = new List<SmogonUsageRankingEntryDto>();
        foreach (var line in content.Split('\n'))
        {
            var trimmedLine = line.Trim();
            if (!trimmedLine.StartsWith('|'))
            {
                continue;
            }

            var columns = trimmedLine.Split('|');
            if (columns.Length < MINIMUM_COLUMN_COUNT)
            {
                continue;
            }

            if (!int.TryParse(columns[RANK_COLUMN].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var rank))
            {
                continue;
            }

            var usageText = columns[USAGE_COLUMN].Trim().TrimEnd('%');
            if (!double.TryParse(usageText, NumberStyles.Float, CultureInfo.InvariantCulture, out var usagePercentage))
            {
                continue;
            }

            int.TryParse(columns[RAW_COUNT_COLUMN].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var rawCount);

            entries.Add(new SmogonUsageRankingEntryDto(rank, columns[POKEMON_COLUMN].Trim(), usagePercentage,
                rawCount));
        }

        return entries;
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
