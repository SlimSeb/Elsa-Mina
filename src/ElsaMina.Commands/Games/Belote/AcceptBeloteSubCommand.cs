using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Lets a user who is not in the game take the seat of a player who asked for a substitute. Triggered
/// from the room sub panel button (<c>belotesubaccept playerid</c>) or in a private message whose target
/// is prefixed with the room id (<c>roomid, playerid</c>).
/// </summary>
[NamedCommand("belotesubaccept", Aliases = ["bsa"])]
public class AcceptBeloteSubCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public AcceptBeloteSubCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string targetPlayerId;
        IRoom room;

        if (context.IsPrivateMessage)
        {
            var parts = context.Target.Split(',', 2);
            if (parts.Length < 2)
            {
                return;
            }

            room = _roomsManager.GetRoom(parts[0].Trim().ToLowerAlphaNum());
            targetPlayerId = parts[1].Trim();
        }
        else
        {
            room = context.Room;
            targetPlayerId = context.Target.Trim();
        }

        if (room?.Game is not IBeloteGame game)
        {
            context.ReplyLocalizedMessage("belote_not_running");
            return;
        }

        var (success, messageKey, args) = await game.AcceptSubAsync(context.Sender, targetPlayerId);
        if (!success)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }
}
