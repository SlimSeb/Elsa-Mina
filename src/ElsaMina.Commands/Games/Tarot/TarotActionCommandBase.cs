using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Base for the in-game tarot action commands (bid, call, discard, play). They work both when
/// typed in the room and when triggered from a panel button (a <c>/botmsg</c> private message
/// whose target is prefixed with the room id, e.g. <c>roomid, garde</c>).
/// </summary>
public abstract class TarotActionCommandBase : Command
{
    private readonly IRoomsManager _roomsManager;

    protected TarotActionCommandBase(IRoomsManager roomsManager)
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

        if (room?.Game is not ITarotGame game)
        {
            return;
        }

        await ExecuteAsync(context, game, argument);
    }

    protected abstract Task ExecuteAsync(IContext context, ITarotGame game, string argument);
}
