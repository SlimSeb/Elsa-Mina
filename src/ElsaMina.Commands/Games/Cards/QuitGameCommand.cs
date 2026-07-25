using ElsaMina.Core.Contexts;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Gives a seat back while the game is still gathering players. Unlike the other lifecycle commands
/// this one also reports success, so the room sees who left.
/// </summary>
public abstract class QuitGameCommand<TGame> : GameLifecycleCommandBase<TGame>
    where TGame : class, ILeavableCardGame
{
    protected override async Task ExecuteAsync(IContext context, TGame game)
    {
        var (_, messageKey, args) = await game.LeaveAsync(context.Sender);
        if (messageKey is not null)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }
}
