using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotquit", Aliases = ["tarotleave", "tq"])]
public class QuitTarotCommand : Command
{
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not ITarotGame game)
        {
            context.ReplyLocalizedMessage("tarot_not_running");
            return;
        }

        var (_, messageKey, args) = await game.LeaveAsync(context.Sender);
        if (messageKey is not null)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }
}
