using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Lets a player in the running game ask to be replaced by a substitute. Running it again cancels the
/// pending request. Works both in the room and from a panel button, which sends a private message
/// whose target is the room id.
/// </summary>
public abstract class RequestSubGameCommand<TGame> : Command where TGame : class, ISubstitutableCardGame
{
    private readonly IRoomsManager _roomsManager;

    protected RequestSubGameCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    /// <summary>
    /// The prefix every resource key of this game starts with, e.g. <c>"tarot"</c>.
    /// </summary>
    protected abstract string ResourcePrefix { get; }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var room = context.IsPrivateMessage
            ? _roomsManager.GetRoom(context.Target.Trim().ToLowerAlphaNum())
            : context.Room;

        if (room?.Game is not TGame game)
        {
            context.ReplyLocalizedMessage($"{ResourcePrefix}_not_running");
            return;
        }

        var (success, messageKey, args) = await game.RequestSubAsync(context.Sender);
        if (!success)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }
}
