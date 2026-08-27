using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

[NamedCommand("lolrank", Aliases = ["lolelo"])]
public class LeagueRankCommand : Command
{
    private const string SOLO_QUEUE = "RANKED_SOLO_5x5";
    private const string FLEX_QUEUE = "RANKED_FLEX_SR";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;
    private readonly ITemplatesManager _templatesManager;

    public LeagueRankCommand(IHttpService httpService,
        IConfiguration configuration,
        ITemplatesManager templatesManager)
    {
        _httpService = httpService;
        _configuration = configuration;
        _templatesManager = templatesManager;
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
            var entriesResponse = await _httpService.SendAsync<List<LeagueEntryDto>>(
                HttpRequest.Get(entriesUrl).WithHeaders(headers), cancellationToken);
            var entries = entriesResponse.Data;

            if (entries == null || entries.Count == 0)
            {
                context.ReplyLocalizedMessage("lolrank_unranked", gameName, tagLine);
                return;
            }

            var soloEntry = entries.FirstOrDefault(e => e.QueueType == SOLO_QUEUE);
            var flexEntry = entries.FirstOrDefault(e => e.QueueType == FLEX_QUEUE);

            var viewModel = new LeagueRankViewModel
            {
                GameName = gameName,
                TagLine = tagLine,
                Platform = platform,
                SoloQueue = BuildQueueViewModel(soloEntry),
                FlexQueue = BuildQueueViewModel(flexEntry),
                Culture = context.Culture
            };

            var template = await _templatesManager.GetTemplateAsync("Misc/LeagueOfLegends/LeagueRank", viewModel);
            context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve League of Legends rank for {RiotId}.", riotId);
            context.ReplyLocalizedMessage("lolrank_error");
        }
    }

    private static LeagueRankQueueViewModel BuildQueueViewModel(LeagueEntryDto entry)
    {
        if (entry == null)
        {
            return new LeagueRankQueueViewModel
            {
                IsUnranked = true,
                EmblemUrl = LeagueApiHelper.GetRankEmblemUrl(null),
                TierColor = LeagueApiHelper.GetTierColor(null)
            };
        }

        var winRate = entry.Wins + entry.Losses > 0
            ? (int)Math.Round(100.0 * entry.Wins / (entry.Wins + entry.Losses))
            : 0;

        return new LeagueRankQueueViewModel
        {
            QueueType = entry.QueueType,
            Tier = entry.Tier,
            Rank = entry.Rank,
            FormattedRank = LeagueApiHelper.FormatTierRank(entry.Tier, entry.Rank),
            LeaguePoints = entry.LeaguePoints,
            Wins = entry.Wins,
            Losses = entry.Losses,
            WinRate = winRate,
            EmblemUrl = LeagueApiHelper.GetRankEmblemUrl(entry.Tier),
            TierColor = LeagueApiHelper.GetTierColor(entry.Tier),
            IsUnranked = false
        };
    }
}