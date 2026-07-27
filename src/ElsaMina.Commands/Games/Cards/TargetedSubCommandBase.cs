using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Base for the substitution commands that name a seat: taking one over, or putting someone up for
/// replacement. Triggered from the room sub panel button (<c>{game}subaccept playerid</c>) or in a
/// private message whose target is prefixed with the room id (<c>roomid, playerid</c>).
/// </summary>
public abstract class TargetedSubCommandBase<TGame> : Command where TGame : class, ICardGame
{
    private readonly IRoomsManager _roomsManager;

    protected TargetedSubCommandBase(IRoomsManager roomsManager)
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

        if (room?.Game is not TGame game)
        {
            context.ReplyLocalizedMessage($"{ResourcePrefix}_not_running");
            return;
        }

        var (success, messageKey, args) = await ExecuteAsync(game, context, targetPlayerId);
        if (!success)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }

    protected abstract Task<(bool Success, string MessageKey, object[] Args)> ExecuteAsync(TGame game,
        IContext context, string targetPlayerId);
}
