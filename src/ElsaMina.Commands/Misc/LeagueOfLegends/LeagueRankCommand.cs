using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

[NamedCommand("lolrank", Aliases = ["lolelo"])]
public class LeagueRankCommand : Command
{
    private const string SOLO_QUEUE = "RANKED_SOLO_5x5";
    private const string FLEX_QUEUE = "RANKED_FLEX_SR";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    public LeagueRankCommand(IHttpService httpService, IConfiguration configuration)
    {
        _httpService = httpService;
        _configuration = configuration;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "lolrank_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.RiotApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Riot API key is empty.");
            context.ReplyLocalizedMessage("lolrank_no_api_key");
            return;
        }

        var parsed = LeagueApiHelper.TryParseInput(context.Target);
        if (parsed == null)
        {
            context.ReplyLocalizedMessage("lolrank_help");
            return;
        }

        var (riotId, platform) = parsed.Value;
        var routing = LeagueApiHelper.GetRouting(platform);
        if (routing == null)
        {
            context.ReplyLocalizedMessage("lolrank_invalid_region", platform);
            return;
        }

        var (gameName, tagLine) = LeagueApiHelper.SplitRiotId(riotId);
        var headers = LeagueApiHelper.BuildHeaders(apiKey);

        try
        {
            var puuid = await LeagueApiHelper.GetPuuidAsync(_httpService, routing, gameName, tagLine, headers,
                cancellationToken);
            if (puuid == null)
            {
                context.ReplyLocalizedMessage("lolrank_player_not_found", riotId);
                return;
            }

            var entriesUrl =
                $"https://{platform}.api.riotgames.com/lol/league/v4/entries/by-puuid/{Uri.EscapeDataString(puuid)}";
            var entriesResponse = await _httpService.GetAsync<List<LeagueEntryDto>>(entriesUrl, headers: headers,
                cancellationToken: cancellationToken);
            var entries = entriesResponse.Data;

            if (entries == null || entries.Count == 0)
            {
                context.ReplyLocalizedMessage("lolrank_unranked", gameName, tagLine);
                return;
            }

            var soloEntry = entries.FirstOrDefault(e => e.QueueType == SOLO_QUEUE);
            var flexEntry = entries.FirstOrDefault(e => e.QueueType == FLEX_QUEUE);

            var lines = new List<string> { context.GetString("lolrank_header", gameName, tagLine) };
            if (soloEntry != null)
                lines.Add(FormatEntry(context, "lolrank_solo", soloEntry));
            if (flexEntry != null)
                lines.Add(FormatEntry(context, "lolrank_flex", flexEntry));
            if (soloEntry == null && flexEntry == null)
                lines.Add(context.GetString("lolrank_unranked_queues"));

            context.Reply(string.Join(" | ", lines), rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve League of Legends rank for {RiotId}.", riotId);
            context.ReplyLocalizedMessage("lolrank_error");
        }
    }

    private static string FormatEntry(IContext context, string queueKey, LeagueEntryDto entry)
    {
        var winRate = entry.Wins + entry.Losses > 0
            ? (int)Math.Round(100.0 * entry.Wins / (entry.Wins + entry.Losses))
            : 0;
        return context.GetString(queueKey, entry.Tier, entry.Rank, entry.LeaguePoints, entry.Wins, entry.Losses,
            winRate);
    }
}
