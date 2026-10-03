using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Badges;

[NamedCommand("givebadge", Aliases = ["give-badge"])]
public class GiveBadgeCommand : Command
{
    private readonly IRoomUserDataService _roomUserDataService;
    private readonly IBotDbContextFactory _dbContextFactory;

    public GiveBadgeCommand(IRoomUserDataService roomUserDataService, IBotDbContextFactory dbContextFactory)
    {
        _roomUserDataService = roomUserDataService;
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "badge_give_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(",");
        if (parts.Length < 2)
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var userId = parts[0].ToLowerAlphaNum();
        var badgeId = parts[1].ToLowerAlphaNum();

        string roomId;
        if (context.IsPrivateMessage)
        {
            if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
            {
                context.ReplyLocalizedMessage("badge_pm_missing_room");
                return;
            }
            roomId = parts[2].Trim().ToLowerAlphaNum();

            if (!await context.HasSufficientRankInRoom(roomId, Rank.Driver, cancellationToken))
            {
                context.ReplyLocalizedMessage("badge_pm_insufficient_rank");
                return;
            }
        }
        else
        {
            roomId = context.RoomId;
        }

        Badge badge = null;
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            badge = await dbContext.Badges.FindAsync([badgeId, roomId], cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while fetching a badge");
        }

        if (badge == null)
        {
            context.ReplyLocalizedMessage("badge_give_could_not_find_badge", badgeId);
            return;
        }

        try
        {
            await _roomUserDataService.GiveBadgeToUserAsync(roomId, userId, badgeId, cancellationToken);
            context.ReplyLocalizedMessage("badge_give_success", userId, badge.Name);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while giving a badge");
            context.ReplyLocalizedMessage("badge_give_error", exception.Message);
        }
    }
}
