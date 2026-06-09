using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Belote;

[NamedCommand("beloteresend", Aliases = ["belotepage", "br"])]
public class ResendBeloteCommand : Command
{
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IBeloteGame game)
        {
            context.ReplyLocalizedMessage("belote_not_running");
            return;
        }

        if (game.Phase == BelotePhase.Lobby)
        {
            context.ReplyLocalizedMessage("belote_resend_not_started");
            return;
        }

        var isPlayer = game.Players.Any(player => player.UserId == context.Sender.UserId);
        if (!isPlayer)
        {
            context.ReplyLocalizedMessage("belote_resend_not_a_player");
            return;
        }

        await game.ResendPlayerPageAsync(context.Sender);
    }
}
