using ElsaMina.Core.Contexts;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// Sends a player their private hand page again, for when they closed it or lost it in the scrollback.
/// </summary>
public abstract class ResendGameCommand<TGame> : GameLifecycleCommandBase<TGame>
    where TGame : class, IResendableCardGame
{
    protected override async Task ExecuteAsync(IContext context, TGame game)
    {
        if (game.IsInLobby)
        {
            context.ReplyLocalizedMessage(Key("resend_not_started"));
            return;
        }

        if (!game.HasPlayer(context.Sender.UserId))
        {
            context.ReplyLocalizedMessage(Key("resend_not_a_player"));
            return;
        }

        await game.ResendPlayerPageAsync(context.Sender);
    }
}
