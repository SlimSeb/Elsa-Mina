using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Lets staff put another player up for substitution without that player having to ask. Triggered in the
/// room (<c>tarotforcesub playerid</c>) or in a private message whose target is prefixed with the room id
/// (<c>roomid, playerid</c>).
/// </summary>
[NamedCommand("tarotforcesub", Aliases = ["tarotsubout", "tfs"])]
public class ForceRequestTarotSubCommand : Command
{
    private readonly IRoomsManager _roomsManager;

    public ForceRequestTarotSubCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Driver;

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

        if (room?.Game is not ITarotGame game)
        {
            context.ReplyLocalizedMessage("tarot_not_running");
            return;
        }

        var (success, messageKey, args) = await game.ForceRequestSubAsync(targetPlayerId);
        if (!success)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }
}
