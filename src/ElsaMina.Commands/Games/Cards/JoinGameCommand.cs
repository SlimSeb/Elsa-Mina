using ElsaMina.Core.Contexts;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Takes a seat in the game currently running in the room. The game itself decides whether there is
/// room left, and says why when there is not.
/// </summary>
public abstract class JoinGameCommand<TGame> : GameLifecycleCommandBase<TGame> where TGame : class, ICardGame
{
    protected override async Task ExecuteAsync(IContext context, TGame game)
    {
        var (success, messageKey, args) = await game.JoinAsync(context.Sender);
        if (!success)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }
}
