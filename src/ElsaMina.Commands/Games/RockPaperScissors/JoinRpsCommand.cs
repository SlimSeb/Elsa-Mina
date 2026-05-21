using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.RockPaperScissors;

[NamedCommand("rpsjoin", Aliases = ["rpsj"])]
public class JoinRpsCommand : Command
{
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IRpsGame rpsGame)
        {
            context.ReplyLocalizedMessage("rps_not_running");
            return;
        }

        var (success, messageKey, args) = await rpsGame.Join(context.Sender.Name);
        if (!success)
            context.ReplyLocalizedMessage(messageKey, args);
    }
}
