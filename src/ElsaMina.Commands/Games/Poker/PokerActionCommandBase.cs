using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Poker;

/// <summary>
/// Base for the in-game poker action commands (fold, check, call, raise). They work both when typed
/// in the room and when triggered from a panel button (a <c>/botmsg</c> private message whose target
/// is the room id, optionally followed by an argument, e.g. <c>roomid, 50</c>).
/// </summary>
public abstract class PokerActionCommandBase : Command
{
    private readonly IRoomsManager _roomsManager;

    protected PokerActionCommandBase(IRoomsManager roomsManager)
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
            room = _roomsManager.GetRoom(parts[0].Trim().ToLowerAlphaNum());
            argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        }
        else
        {
            room = context.Room;
            argument = context.Target.Trim();
        }

        if (room?.Game is not IPokerGame game)
        {
            return;
        }

        await ExecuteAsync(context, game, argument);
    }

    protected abstract Task ExecuteAsync(IContext context, IPokerGame game, string argument);
}
