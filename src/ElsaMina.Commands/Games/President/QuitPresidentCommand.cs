using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

[NamedCommand("presidentquit", Aliases = ["presidentleave", "prq"])]
public class QuitPresidentCommand : Command
{
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IPresidentGame game)
        {
            context.ReplyLocalizedMessage("president_not_running");
            return;
        }

        var (_, messageKey, args) = await game.LeaveAsync(context.Sender);
        if (messageKey is not null)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }
}
