using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

[NamedCommand("lolhistory", Aliases = ["lolh", "lolgames"])]
public class LeagueOfLegendsHistoryCommand : Command
{
    private static readonly Dictionary<int, string> QUEUE_NAMES =
        new()
        {
            [420] = "Solo",
            [440] = "Flex",
            [400] = "Normal",
            [430] = "Blind",
            [450] = "ARAM",
            [1700] = "Arena",
            [0] = "Custom"
        };

    private const int HISTORY_COUNT = 5;

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    public LeagueOfLegendsHistoryCommand(IHttpService httpService, IConfiguration configuration)
    {
        _httpService = httpService;
        _configuration = configuration;
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
            var matchIdsResponse = await _httpService.GetAsync<List<string>>(matchIdsUrl,
                new Dictionary<string, string> { ["start"] = "0", ["count"] = HISTORY_COUNT.ToString() },
                headers: headers, cancellationToken: cancellationToken);
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
                return _httpService.GetAsync<MatchDto>(matchUrl, headers: headers,
                    cancellationToken: cancellationToken);
            });
            var matchResponses = await Task.WhenAll(matchTasks);

            var entries = new List<string>();
            foreach (var match in matchResponses.Select(matchResponse => matchResponse.Data))
            {
                var participant = match?.Info?.Participants?.FirstOrDefault(p => p.Puuid == puuid);
                if (participant == null)
                    continue;

                var queueName = QUEUE_NAMES.GetValueOrDefault(match.Info.QueueId, "?");
                var durationMinutes = match.Info.GameDuration / 60;
                var cs = participant.TotalMinionsKilled + participant.NeutralMinionsKilled;
                var resultKey = participant.Win ? "lolhistory_win" : "lolhistory_loss";
                entries.Add(context.GetString("lolhistory_game_entry",
                    context.GetString(resultKey),
                    participant.ChampionName,
                    participant.Kills,
                    participant.Deaths,
                    participant.Assists,
                    cs,
                    queueName,
                    durationMinutes));
            }

            if (entries.Count == 0)
            {
                context.ReplyLocalizedMessage("lolhistory_no_games", gameName, tagLine);
                return;
            }

            var header = context.GetString("lolhistory_header", gameName, tagLine);
            context.Reply($"!code {header}\n{string.Join("\n", entries)}", rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve match history for {RiotId}.", riotId);
            context.ReplyLocalizedMessage("lolhistory_error");
        }
    }
}