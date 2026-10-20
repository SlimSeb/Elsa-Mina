using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Development;

[NamedCommand("ping", Aliases = ["tdt"])]
public class Ping : Command
{
    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRankInPrivateMessage => Rank.Regular;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        context.Reply("pong", rankAware: true);
        return Task.CompletedTask;
    }
}