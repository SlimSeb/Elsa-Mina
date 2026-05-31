using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Poker;

[NamedCommand("pokerstart", Aliases = ["pokerbegin"])]
public class BeginPokerCommand : Command
{
    public override Rank RequiredRank => Rank.Voiced;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IPokerGame game)
        {
            context.ReplyLocalizedMessage("poker_not_running");
            return;
        }

        await game.StartAsync(context.Sender);
    }
}
