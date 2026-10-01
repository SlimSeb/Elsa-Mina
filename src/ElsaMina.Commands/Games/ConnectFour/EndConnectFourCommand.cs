using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.ConnectFour;

[NamedCommand("end-connect-four", Aliases = ["c4end", "c4-end"])]
public class EndConnectFourCommand : GameCommand
{
    public override Rank RequiredRank => Rank.Voiced;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is IConnectFourGame connectFourGame)
        {
            connectFourGame.Cancel();
            context.ReplyLocalizedMessage("c4_game_cancelled");
        }
        else
        {
            context.ReplyLocalizedMessage("c4_game_ongoing_game");
        }
        
        return Task.CompletedTask;
    }
}