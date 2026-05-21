using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.RockPaperScissors;

[NamedCommand("rpsend")]
public class EndRpsCommand : Command
{
    public override Rank RequiredRank => Rank.Voiced;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IRpsGame rpsGame)
        {
            context.ReplyLocalizedMessage("rps_not_running");
            return Task.CompletedTask;
        }

        rpsGame.Cancel();
        context.ReplyLocalizedMessage("rps_game_cancelled");
        return Task.CompletedTask;
    }
}
