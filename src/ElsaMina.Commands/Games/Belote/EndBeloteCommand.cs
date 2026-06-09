using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Belote;

[NamedCommand("beloteend")]
public class EndBeloteCommand : Command
{
    public override Rank RequiredRank => Rank.Voiced;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IBeloteGame game)
        {
            context.ReplyLocalizedMessage("belote_not_running");
            return Task.CompletedTask;
        }

        game.Cancel();
        context.ReplyLocalizedMessage("belote_game_cancelled");
        return Task.CompletedTask;
    }
}
