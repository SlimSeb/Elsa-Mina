using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Dolls;

[NamedCommand("givedoll", Aliases = ["give-doll"])]
public class GiveDollCommand : Command
{
    private readonly IDollService _dollService;
    private readonly IRoomUserDataService _roomUserDataService;

    public GiveDollCommand(IDollService dollService, IRoomUserDataService roomUserDataService)
    {
        _dollService = dollService;
        _roomUserDataService = roomUserDataService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "doll_give_help_message";

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

        Doll doll;
        try
        {
            doll = await _dollService.GetDollAsync(dollId, cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not read the doll catalogue");
            context.ReplyLocalizedMessage("doll_catalogue_unavailable", exception.Message);
            return;
        }

        if (doll == null)
        {
            context.ReplyLocalizedMessage("doll_not_found", dollId);
            return;
        }

        if (await _dollService.IsDollOwnedByUserAsync(roomId, userId, dollId, cancellationToken))
        {
            context.ReplyLocalizedMessage("doll_give_already_owned", userId, doll.Name);
            return;
        }

        try
        {
            await _roomUserDataService.GiveDollToUserAsync(roomId, userId, dollId, cancellationToken);
            context.ReplyLocalizedMessage("doll_give_success", userId, doll.Name);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while giving a doll");
            context.ReplyLocalizedMessage("doll_give_failure", exception.Message);
        }
    }
}
