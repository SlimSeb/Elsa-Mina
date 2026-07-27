using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Lets a user who is not in the game take the seat of a player who asked for a substitute.
/// </summary>
public abstract class AcceptSubGameCommand<TGame> : TargetedSubCommandBase<TGame>
    where TGame : class, ISubstitutableCardGame
{
    protected AcceptSubGameCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override Task<(bool Success, string MessageKey, object[] Args)> ExecuteAsync(TGame game,
        IContext context, string targetPlayerId) => game.AcceptSubAsync(context.Sender, targetPlayerId);
}
