using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

[NamedCommand("lolhistory", Aliases = ["lolh", "lolgames"])]
public class LeagueOfLegendsHistoryCommand : Command
{
    private static readonly Dictionary<int, string> QUEUE_NAMES =
        new()
        {
            [420] = "Ranked Solo",
            [440] = "Ranked Flex",
            [400] = "Draft Pick",
            [430] = "Blind Pick",
            [450] = "ARAM",
            [490] = "Quickplay",
            [1700] = "Arena",
            [0] = "Custom"
        };

    private const int HISTORY_COUNT = 5;

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;
    private readonly ITemplatesManager _templatesManager;

    public LeagueOfLegendsHistoryCommand(IHttpService httpService,
        IConfiguration configuration,
        ITemplatesManager templatesManager)
    {
        _httpService = httpService;
        _configuration = configuration;
        _templatesManager = templatesManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "lolhistory_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.RiotApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Riot API key is empty.");
            context.ReplyLocalizedMessage("lolhistory_no_api_key");
            return;
        }

        var parsed = LeagueApiHelper.TryParseInput(context.Target);
        if (parsed == null)
        {
            context.ReplyLocalizedMessage("lolhistory_help");
            return;
        }

        var (riotId, platform) = parsed.Value;
        var routing = LeagueApiHelper.GetRouting(platform);
        if (routing == null)
        {
            context.ReplyLocalizedMessage("lolhistory_invalid_region", platform);
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
                context.ReplyLocalizedMessage("lolhistory_player_not_found", riotId);
                return;
            }

            var matchIdsUrl =
                $"https://{routing}.api.riotgames.com/lol/match/v5/matches/by-puuid/{Uri.EscapeDataString(puuid)}/ids";
            var matchIdsResponse = await _httpService.SendAsync<List<string>>(
                HttpRequest.Get(matchIdsUrl)
                    .WithQueryParameter("start", "0")
                    .WithQueryParameter("count", HISTORY_COUNT.ToString())
                    .WithHeaders(headers),
                cancellationToken);
            var matchIds = matchIdsResponse.Data;

            if (matchIds == null || matchIds.Count == 0)
            {
                context.ReplyLocalizedMessage("lolhistory_no_games", gameName, tagLine);
                return;
            }

            var matchTasks = matchIds.Select(matchId =>
            {
                var matchUrl =
                    $"https://{routing}.api.riotgames.com/lol/match/v5/matches/{Uri.EscapeDataString(matchId)}";
                return _httpService.SendAsync<MatchDto>(
                    HttpRequest.Get(matchUrl).WithHeaders(headers), cancellationToken);
            });
            var matchResponses = await Task.WhenAll(matchTasks);

            var games = new List<LeagueHistoryGameViewModel>();
            foreach (var match in matchResponses.Select(matchResponse => matchResponse.Data))
            {
                var participant = match?.Info?.Participants?.FirstOrDefault(p => p.Puuid == puuid);
                if (participant == null)
                {
                    continue;
                }

                var queueName = QUEUE_NAMES.GetValueOrDefault(match.Info.QueueId, "Other");
                var durationMinutes = match.Info.GameDuration / 60;
                var cs = participant.TotalMinionsKilled + participant.NeutralMinionsKilled;
                var timestamp = match.Info.GameEndTimestamp > 0
                    ? match.Info.GameEndTimestamp
                    : match.Info.GameCreation;
                var gameDate = timestamp > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                    : DateTimeOffset.UtcNow;
                var formattedDate = gameDate.ToString("d", context.Culture);

                games.Add(new LeagueHistoryGameViewModel
                {
                    ChampionName = participant.ChampionName,
                    ChampionId = participant.ChampionId,
                    ChampionIconUrl = LeagueApiHelper.GetChampionIconUrl(participant.ChampionId, participant.ChampionName),
                    Win = participant.Win,
                    Kills = participant.Kills,
                    Deaths = participant.Deaths,
                    Assists = participant.Assists,
                    KdaRatio = LeagueApiHelper.CalculateKdaRatio(participant.Kills, participant.Deaths, participant.Assists),
                    Cs = cs,
                    CsPerMinute = LeagueApiHelper.CalculateCsPerMinute(cs, durationMinutes),
                    QueueName = queueName,
                    DurationMinutes = durationMinutes,
                    GameDate = TimeZoneInfo.ConvertTime(gameDate, context.Room.TimeZone),
                    FormattedDate = formattedDate
                });
            }

            if (games.Count == 0)
            {
                context.ReplyLocalizedMessage("lolhistory_no_games", gameName, tagLine);
                return;
            }

            var viewModel = new LeagueHistoryViewModel
            {
                GameName = gameName,
                TagLine = tagLine,
                Platform = platform,
                Games = games,
                Culture = context.Culture
            };

            var template = await _templatesManager.GetTemplateAsync("Misc/LeagueOfLegends/LeagueHistory", viewModel);
            context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve match history for {RiotId}.", riotId);
            context.ReplyLocalizedMessage("lolhistory_error");
        }
    }
}