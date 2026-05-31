using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Poker;

[NamedCommand("pokerjoin", Aliases = ["pj"])]
public class JoinPokerCommand : Command
{
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IPokerGame game)
        {
            context.ReplyLocalizedMessage("poker_not_running");
            return;
        }

        var (success, messageKey, args) = await game.JoinAsync(context.Sender);
        if (!success)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }
}
