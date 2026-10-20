using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Dolls;

[NamedCommand("takedoll", Aliases = ["take-doll"])]
public class TakeDollCommand : Command
{
    private readonly IRoomUserDataService _roomUserDataService;

    public TakeDollCommand(IRoomUserDataService roomUserDataService)
    {
        _roomUserDataService = roomUserDataService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "doll_take_help_message";

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
        var dollId = parts[1].ToLowerAlphaNum();

        var roomId = GetTargetRoomId(context);
        if (roomId == null)
        {
            context.ReplyLocalizedMessage("doll_pm_missing_room");
            return;
        }

        try
        {
            await _roomUserDataService.TakeDollFromUserAsync(roomId, userId, dollId, cancellationToken);
            context.ReplyLocalizedMessage("doll_take_success", userId, dollId);
        }
        catch (ArgumentException)
        {
            context.ReplyLocalizedMessage("doll_take_not_owned", userId, dollId);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while taking a doll");
            context.ReplyLocalizedMessage("doll_take_failure", exception.Message);
        }
    }
}
