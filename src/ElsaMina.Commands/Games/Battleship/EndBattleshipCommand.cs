using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Battleship;

[NamedCommand("end-battleship", Aliases = ["bsend", "bs-end"])]
public class EndBattleshipCommand : Command
{
    public override Rank RequiredRank => Rank.Voiced;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is IBattleshipGame battleshipGame)
        {
            battleshipGame.Cancel();
            context.ReplyLocalizedMessage("battleship_game_cancelled");
        }
        else
        {
            context.ReplyLocalizedMessage("battleship_game_ongoing_game");
        }

        return Task.CompletedTask;
    }
}
