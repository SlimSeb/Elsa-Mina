using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotresend", Aliases = ["tarotpage", "tr"])]
public class ResendTarotCommand : Command
{
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not ITarotGame game)
        {
            context.ReplyLocalizedMessage("tarot_not_running");
            return;
        }

        if (game.Phase == TarotPhase.Lobby)
        {
            context.ReplyLocalizedMessage("tarot_resend_not_started");
            return;
        }

        var isPlayer = game.Players.Any(player => player.UserId == context.Sender.UserId);
        if (!isPlayer)
        {
            context.ReplyLocalizedMessage("tarot_resend_not_a_player");
            return;
        }

        await game.ResendPlayerPageAsync(context.Sender);
    }
}
