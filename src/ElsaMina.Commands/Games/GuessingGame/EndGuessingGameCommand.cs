using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.GuessingGame;

[NamedCommand("endguessinggame", Aliases = ["endcountriesgame"])]
public class EndGuessingGameCommand : GameCommand
{
    public override Rank RequiredRank => Rank.Voiced;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is IGuessingGame guessingGame)
        {
            guessingGame.StopGame();
            context.ReplyLocalizedMessage("end_guessing_game_success");
            return Task.CompletedTask;
        }

        context.ReplyLocalizedMessage("end_guessing_game_no_game");
        return Task.CompletedTask;
    }
}