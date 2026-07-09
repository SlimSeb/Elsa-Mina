using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Scattergories;

[NamedCommand("endscattergories", Aliases = ["endscatt", "scattend"])]
public class EndScattergoriesCommand : Command
{
    public override Rank RequiredRank => Rank.Driver;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.Room?.Game is not IScattergoriesGame scattergories)
        {
            context.ReplyLocalizedMessage("scattergories_not_running");
            return Task.CompletedTask;
        }

        scattergories.Cancel();
        context.ReplyLocalizedMessage("scattergories_cancelled");
        return Task.CompletedTask;
    }
}
