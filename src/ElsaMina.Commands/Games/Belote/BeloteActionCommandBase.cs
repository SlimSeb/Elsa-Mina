using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Belote;

/// <summary>
/// Base for the in-game belote action commands (bid, play). They work both when typed in the room and
/// when triggered from a panel button (a <c>/botmsg</c> private message whose target is prefixed with
/// the room id, e.g. <c>roomid, ah</c>).
/// </summary>
public abstract class BeloteActionCommandBase : Command
{
    private readonly IRoomsManager _roomsManager;

    protected BeloteActionCommandBase(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string argument;
        IRoom room;

        if (context.IsPrivateMessage)
        {
            var parts = context.Target.Split(',', 2);
            if (parts.Length < 2)
            {
                return;
            }

            room = _roomsManager.GetRoom(parts[0].Trim().ToLowerAlphaNum());
            argument = parts[1].Trim();
        }
        else
        {
            room = context.Room;
            argument = context.Target.Trim();
        }

        if (room?.Game is not IBeloteGame game)
        {
            return;
        }

        await ExecuteAsync(context, game, argument);
    }

    protected abstract Task ExecuteAsync(IContext context, IBeloteGame game, string argument);
}
