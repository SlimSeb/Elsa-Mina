using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

[NamedCommand("presidentjoin", Aliases = ["prj"])]
public class JoinPresidentCommand : Command
{
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IPresidentGame game)
        {
            context.ReplyLocalizedMessage("president_not_running");
            return;
        }

        var (success, messageKey, args) = await game.JoinAsync(context.Sender);
        if (!success)
        {
            context.ReplyLocalizedMessage(messageKey, args);
        }
    }
}
