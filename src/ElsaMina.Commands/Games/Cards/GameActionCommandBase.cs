using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Base for the in-game action commands of a card game (bid, play, discard, fold...). They work both
/// when typed in the room and when triggered from a panel button, which sends a <c>/botmsg</c> private
/// message whose target is prefixed with the room id, e.g. <c>roomid, garde</c>.
/// </summary>
/// <typeparam name="TGame">The game interface the room's running game must implement.</typeparam>
public abstract class GameActionCommandBase<TGame> : Command where TGame : class, IGame
{
    private readonly IRoomsManager _roomsManager;

    protected GameActionCommandBase(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    /// <summary>
    /// Whether the private message form has to carry an argument after the room id. Commands whose
    /// buttons send the room id on its own (poker's fold, check and call) turn this off.
    /// </summary>
    protected virtual bool RequiresArgument => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string argument;
        IRoom room;

        if (context.IsPrivateMessage)
        {
            var parts = context.Target.Split(',', 2);
            if (RequiresArgument && parts.Length < 2)
            {
                return;
            }

            room = _roomsManager.GetRoom(parts[0].Trim().ToLowerAlphaNum());
            argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        }
        else
        {
            room = context.Room;
            argument = context.Target.Trim();
        }

        if (room?.Game is not TGame game)
        {
            return;
        }

        await ExecuteAsync(context, game, argument);
    }

    protected abstract Task ExecuteAsync(IContext context, TGame game, string argument);
}
