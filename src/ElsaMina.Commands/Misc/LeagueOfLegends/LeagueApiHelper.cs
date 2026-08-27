using ElsaMina.Core.Services.Http;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public static class LeagueApiHelper
{
    private const string AMERICAS = "americas";
    private const string EUROPE = "europe";
    private const string ASIA = "asia";
    private const string SEA = "sea";

    private static readonly Dictionary<string, string> PLATFORM_TO_ROUTING =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["na1"] = AMERICAS, ["na"] = AMERICAS,
            ["br1"] = AMERICAS, ["br"] = AMERICAS,
            ["la1"] = AMERICAS, ["la2"] = AMERICAS,
            ["euw1"] = EUROPE, ["euw"] = EUROPE,
            ["eun1"] = EUROPE, ["eune"] = EUROPE,
            ["tr1"] = EUROPE, ["tr"] = EUROPE,
            ["ru"] = EUROPE,
            ["kr"] = ASIA,
            ["jp1"] = ASIA, ["jp"] = ASIA,
            ["oc1"] = SEA, ["oce"] = SEA,
            ["sg2"] = SEA, ["tw2"] = SEA, ["vn2"] = SEA,
        };

    private const string DEFAULT_PLATFORM = "euw1";

    // Returns (riotId, platform) or null when target has no valid Riot ID.
    public static (string RiotId, string Platform)? TryParseInput(string target)
    {
        var parts = target.Trim().Split(',', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].Contains('#'))
        {
            return null;
        }

        return (parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : DEFAULT_PLATFORM);
    }

    public static string GetRouting(string platform) =>
        PLATFORM_TO_ROUTING.TryGetValue(platform, out var routing) ? routing : null;

    public static (string GameName, string TagLine) SplitRiotId(string riotId)
    {
        var index = riotId.IndexOf('#');
        return (riotId[..index], riotId[(index + 1)..]);
    }

    public static IDictionary<string, string> BuildHeaders(string apiKey) =>
        new Dictionary<string, string> { ["X-Riot-Token"] = apiKey };

    public static async Task<string> GetPuuidAsync(IHttpService httpService, string routing,
        string gameName, string tagLine, IDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://{routing}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{Uri.EscapeDataString(gameName)}/{Uri.EscapeDataString(tagLine)}";
        var response = await httpService.SendAsync<RiotAccountDto>(
            HttpRequest.Get(url).WithHeaders(headers), cancellationToken);
        var puuid = response.Data?.Puuid;
        return string.IsNullOrEmpty(puuid) ? null : puuid;
    }

    public static string GetRankEmblemUrl(string tier)
    {
        if (string.IsNullOrWhiteSpace(tier))
        {
            return "https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-static-assets/global/default/images/ranked-mini-crests/unranked.png";
        }

        return $"https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-static-assets/global/default/images/ranked-emblem/emblem-{tier.Trim().ToLowerInvariant()}.png";
    }

    public static string GetChampionIconUrl(int championId, string championName = null)
    {
        if (championId > 0)
        {
            return $"https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champion-icons/{championId}.png";
        }

        if (!string.IsNullOrWhiteSpace(championName))
        {
            return $"https://ddragon.leagueoflegends.com/cdn/14.24.1/img/champion/{Uri.EscapeDataString(championName)}.png";
        }

        return "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champion-icons/-1.png";
    }

    public static string GetTierColor(string tier) => tier?.ToUpperInvariant() switch
    {
        "IRON" => "#9e948d",
        "BRONZE" => "#cd885d",
        "SILVER" => "#a3b8c8",
        "GOLD" => "#eec152",
        "PLATINUM" => "#49c5b1",
        "EMERALD" => "#32d583",
        "DIAMOND" => "#6ba6ff",
        "MASTER" => "#c084fc",
        "GRANDMASTER" => "#f87171",
        "CHALLENGER" => "#fde047",
        _ => "#8a96a3"
    };

    public static string FormatTierRank(string tier, string rank)
    {
        if (string.IsNullOrWhiteSpace(tier))
        {
            return "Unranked";
        }

        var upperTier = tier.ToUpperInvariant();
        if (upperTier is "MASTER" or "GRANDMASTER" or "CHALLENGER" || string.IsNullOrWhiteSpace(rank))
        {
            return upperTier;
        }

        return $"{upperTier} {rank.ToUpperInvariant()}";
    }

    public static string CalculateKdaRatio(int kills, int deaths, int assists)
    {
        if (deaths == 0)
        {
            return "Perfect";
        }

        return ((double)(kills + assists) / deaths).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string CalculateCsPerMinute(int cs, int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            return "0.0";
        }

        return ((double)cs / durationMinutes).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }
}