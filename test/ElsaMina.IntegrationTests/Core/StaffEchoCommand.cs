using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.IntegrationTests.Core;

[NamedCommand("staffecho")]
internal sealed class StaffEchoCommand : Command
{
    private readonly ICommandExecutionProbe _probe;

    public StaffEchoCommand(ICommandExecutionProbe probe)
    {
        _probe = probe;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Driver;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var output = $"staffecho:{context.Target}";
        context.Reply(output);
        _probe.MarkExecuted(output);
        return Task.CompletedTask;
    }
}
