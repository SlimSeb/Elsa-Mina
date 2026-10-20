using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Badges;

[NamedCommand("takebadge", Aliases = ["take-badge", "take-trophy", "taketrophy"])]
public class TakeBadgeCommand : Command
{
    private readonly IRoomUserDataService _roomUserDataService;

    public TakeBadgeCommand(IRoomUserDataService roomUserDataService)
    {
        _roomUserDataService = roomUserDataService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "takebadge_help_message";

    // En PM la room est le 3e argument
    public override string GetTargetRoomId(IContext context) =>
        context.IsPrivateMessage ? context.GetRoomIdArgument(2) : context.RoomId;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(",");
        if (parts.Length < 2)
        {
            context.ReplyLocalizedMessage(HelpMessageKey);
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

        try
        {
            await _roomUserDataService.TakeBadgeFromUserAsync(roomId, userId, badgeId, cancellationToken);
            context.ReplyLocalizedMessage("takebadge_success", userId, badgeId);
        }
        catch (ArgumentException)
        {
            context.ReplyLocalizedMessage("takebadge_badge_doesnt_exist", userId, badgeId);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while taking badge");
            context.ReplyLocalizedMessage("takebadge_failure", exception.Message);
        }
    }
}
