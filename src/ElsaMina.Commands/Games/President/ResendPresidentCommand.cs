using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

[NamedCommand("presidentresend", Aliases = ["presidentpage", "prr"])]
public class ResendPresidentCommand : Command
{
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IPresidentGame game)
        {
            context.ReplyLocalizedMessage("president_not_running");
            return;
        }

        if (game.Phase == PresidentPhase.Lobby)
        {
            context.ReplyLocalizedMessage("president_resend_not_started");
            return;
        }

        var isPlayer = game.Players.Any(player => player.UserId == context.Sender.UserId);
        if (!isPlayer)
        {
            context.ReplyLocalizedMessage("president_resend_not_a_player");
            return;
        }

        await game.ResendPlayerPageAsync(context.Sender);
    }
}
