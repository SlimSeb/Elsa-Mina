using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Tournaments.Betting.Leaderboard;

[NamedCommand("betleaderboard", Aliases = ["betboard", "topbettors", "bettop"])]
public class TopBettorsCommand : Command
{
    private const int TOP_COUNT = 30;

    private readonly IBotDbContextFactory _botDbContextFactory;
    private readonly ITemplatesManager _templatesManager;
    private readonly IRoomsManager _roomsManager;

    public TopBettorsCommand(IBotDbContextFactory botDbContextFactory, ITemplatesManager templatesManager,
        IRoomsManager roomsManager)
    {
        _botDbContextFactory = botDbContextFactory;
        _templatesManager = templatesManager;
        _roomsManager = roomsManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "top_bettors_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var roomId = string.IsNullOrEmpty(context.Target)
                ? context.RoomId
                : context.Target.ToLowerAlphaNum();

            await using var dbContext = await _botDbContextFactory.CreateDbContextAsync(cancellationToken);
            var topRecords = await dbContext.BetRecords
                .Where(record => record.RoomId == roomId)
                .Include(record => record.RoomUser)
                .ThenInclude(roomUser => roomUser.User)
                .OrderByDescending(record => record.CorrectBetsCount)
                .ThenByDescending(record => record.TotalBetsCount)
                .Take(TOP_COUNT)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (topRecords.Count == 0)
            {
                context.ReplyRankAwareLocalizedMessage("top_bettors_no_data");
                return;
            }

            var topList = topRecords
                .Select((record, i) => new TopBettorsEntry(
                    Rank: i + 1,
                    UserId: record.UserId,
                    UserName: record.RoomUser?.User?.UserName ?? record.UserId,
                    CorrectBetsCount: record.CorrectBetsCount,
                    TotalBetsCount: record.TotalBetsCount))
                .ToList();

            var roomLabel = _roomsManager.GetRoom(roomId)?.Name ?? roomId;
            var table = await _templatesManager.GetTemplateAsync(
                "Tournaments/Betting/Leaderboard/TopBettorsTable",
                new TopBettorsViewModel
                {
                    Culture = context.Culture,
                    Room = roomLabel,
                    TopList = topList
                });

            context.ReplyHtml(
                table.RemoveNewlines().RemoveWhitespacesBetweenTags().CollapseAttributeWhitespace(),
                rankAware: true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to get top bettors");
            context.ReplyRankAwareLocalizedMessage("top_bettors_error");
        }
    }
}
