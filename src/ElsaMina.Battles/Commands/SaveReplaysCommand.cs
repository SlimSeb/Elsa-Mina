using ElsaMina.Battles.Replays;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Battles.Commands;

[NamedCommand("savereplays")]
public class SaveReplaysCommand : Command
{
    private readonly IReplaySavingService _replaySavingService;

    public SaveReplaysCommand(IReplaySavingService replaySavingService)
    {
        _replaySavingService = replaySavingService;
    }

    public override bool IsWhitelistOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var argument = context.Target?.Trim().ToLowerInvariant();
        switch (argument)
        {
            case null or "" or "on":
                _replaySavingService.Start();
                context.Reply($"Battles are now saved to {_replaySavingService.Status.Directory} when they end: " +
                              "Showdown's replay (.json) and everything the bot received, requests included (.log).");
                break;
            case "off" or "stop":
                _replaySavingService.Stop();
                var status = _replaySavingService.Status;
                context.Reply($"Battles are no longer saved ({status.SavedCount} replays saved to {status.Directory}).");
                break;
            default:
                context.Reply("Usage: savereplays [on|off]. battlestatus shows how many were saved, and where.");
                break;
        }

        return Task.CompletedTask;
    }
}
