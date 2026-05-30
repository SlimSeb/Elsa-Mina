using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotend")]
public class EndTarotCommand : Command
{
    public override Rank RequiredRank => Rank.Voiced;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not ITarotGame game)
        {
            context.ReplyLocalizedMessage("tarot_not_running");
            return Task.CompletedTask;
        }

        game.Cancel();
        context.ReplyLocalizedMessage("tarot_game_cancelled");
        return Task.CompletedTask;
    }
}
