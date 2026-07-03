using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Battles.Commands;

[NamedCommand("laddering")]
public class LadderingCommand : Command
{
    private readonly ILadderingService _ladderingService;

    public LadderingCommand(ILadderingService ladderingService)
    {
        _ladderingService = ladderingService;
    }

    public override bool IsWhitelistOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var format = context.Target?.Trim();

        if (string.IsNullOrWhiteSpace(format) || format is "off" or "stop")
        {
            if (_ladderingService.IsLaddering)
            {
                _ladderingService.Stop();
                context.Reply("Laddering stopped.");
            }
            else
            {
                context.Reply("Usage: laddering <format> (or \"laddering off\" to stop).");
            }

            return Task.CompletedTask;
        }

        _ladderingService.Start(format);
        context.Reply($"Laddering started in format {format}. A new battle will be searched after each one ends.");
        return Task.CompletedTask;
    }
}
