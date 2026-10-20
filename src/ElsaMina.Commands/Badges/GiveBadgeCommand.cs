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

    // En PM la room est le 3e argument
    public override string GetTargetRoomId(IContext context) =>
        context.IsPrivateMessage ? context.GetRoomIdArgument(2) : context.RoomId;

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

        var roomId = GetTargetRoomId(context);
        if (roomId == null)
        {
            context.ReplyLocalizedMessage("badge_pm_missing_room");
            return;
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
