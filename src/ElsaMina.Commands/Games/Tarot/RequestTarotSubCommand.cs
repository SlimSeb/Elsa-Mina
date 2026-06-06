using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Lets a player in the running game ask to be replaced by a substitute. Running it again cancels the
/// pending request. Works both in the room and from a panel button (a private message whose target is
/// the room id).
/// </summary>
[NamedCommand("tarotsub", Aliases = ["tsub"])]
public class RequestTarotSubCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public RequestTarotSubCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var room = context.IsPrivateMessage
            ? _roomsManager.GetRoom(context.Target.Trim().ToLowerAlphaNum())
            : context.Room;

        if (room?.Game is not ITarotGame game)
        {
            context.ReplyLocalizedMessage("tarot_not_running");
            return;
        }

        var (success, messageKey, args) = await game.RequestSubAsync(context.Sender);
        if (!success)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }
}
