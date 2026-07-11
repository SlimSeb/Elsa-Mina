using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

[NamedCommand("presidentend")]
public class EndPresidentCommand : Command
{
    public override Rank RequiredRank => Rank.Voiced;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IPresidentGame game)
        {
            context.ReplyLocalizedMessage("president_not_running");
            return;
        }

        await game.CancelAsync();
        context.ReplyLocalizedMessage("president_game_cancelled");
    }
}
