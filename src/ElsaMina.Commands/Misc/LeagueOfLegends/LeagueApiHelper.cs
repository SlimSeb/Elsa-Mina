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
        var response = await httpService.GetAsync<RiotAccountDto>(url, headers: headers,
            cancellationToken: cancellationToken);
        var puuid = response.Data?.Puuid;
        return string.IsNullOrEmpty(puuid) ? null : puuid;
    }
}