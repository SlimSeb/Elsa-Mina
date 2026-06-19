using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Chess;

[NamedCommand("end-chess", Aliases = ["chessend", "chess-end"])]
public class EndChessCommand : Command
{
    public override Rank RequiredRank => Rank.Voiced;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is IChessGame chessGame)
        {
            chessGame.Cancel();
            context.ReplyLocalizedMessage("chess_game_cancelled");
        }
        else
        {
            context.ReplyLocalizedMessage("chess_game_ongoing_game");
        }

        return Task.CompletedTask;
    }
}
