using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Tournaments.Leaderboard;

[NamedCommand("tourtop", Aliases = ["tourclassement", "tournamentrecords", "tourleaderboard", "board"])]
public class TopTournamentPlayersCommand : Command
{
    private const int TOP_COUNT = 50;

    private readonly IBotDbContextFactory _botDbContextFactory;
    private readonly ITemplatesManager _templatesManager;
    private readonly IRoomsManager _roomsManager;

    public TopTournamentPlayersCommand(IBotDbContextFactory botDbContextFactory, ITemplatesManager templatesManager,
        IRoomsManager roomsManager)
    {
        _botDbContextFactory = botDbContextFactory;
        _templatesManager = templatesManager;
        _roomsManager = roomsManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "top_tournament_players_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var roomId = string.IsNullOrEmpty(context.Target)
                ? context.RoomId
                : context.Target.ToLowerAlphaNum();

            await using var dbContext = await _botDbContextFactory.CreateDbContextAsync(cancellationToken);
            var topRecords = await dbContext.TournamentRecords
                .Where(record => record.RoomId == roomId)
                .Include(record => record.RoomUser.User)
                .OrderByDescending(record => record.WinsCount)
                .ThenByDescending(record => record.RunnerUpCount)
                .ThenByDescending(record => record.ThirdPlaceCount)
                .ThenByDescending(record => record.WonGames)
                .Take(TOP_COUNT)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (topRecords.Count == 0)
            {
                context.ReplyRankAwareLocalizedMessage("top_tournament_players_no_data");
                return;
            }

            var topList = topRecords
                .Select((record, i) => new TopTournamentPlayersEntry(
                    Rank: i + 1,
                    UserId: record.UserId,
                    UserName: record.RoomUser?.User?.UserName ?? record.UserId,
                    WinsCount: record.WinsCount,
                    RunnerUpCount: record.RunnerUpCount,
                    ThirdPlaceCount: record.ThirdPlaceCount,
                    TournamentsEnteredCount: record.TournamentsEnteredCount,
                    WonGames: record.WonGames,
                    PlayedGames: record.PlayedGames))
                .ToList();

            var roomLabel = _roomsManager.GetRoom(roomId)?.Name ?? roomId;
            var table = await _templatesManager.GetTemplateAsync("Tournaments/Leaderboard/TopTournamentPlayersTable",
                new TopTournamentPlayersViewModel
                {
                    Culture = context.Culture,
                    Room = roomLabel,
                    TopList = topList
                });
            var footer = await _templatesManager.GetTemplateAsync("Tournaments/Leaderboard/TopTournamentPlayersFooter",
                new LocalizableViewModel
                {
                    Culture = context.Culture
                });

            context.ReplyHtml(
                table.RemoveNewlines().RemoveWhitespacesBetweenTags().CollapseAttributeWhitespace(),
                rankAware: true);
            context.ReplyHtml(
                footer.RemoveNewlines().CollapseAttributeWhitespace(),
                rankAware: true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to get top tournament players");
            context.ReplyRankAwareLocalizedMessage("top_tournament_players_error");
        }
    }
}