using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Showdown.Ladder.EloHistory;

[NamedCommand("untrackeloprogression", "untrackelo", "elountrack")]
public class UntrackEloProgressionCommand : Command
{
    private readonly IEloProgressionManager _eloProgressionManager;

    public UntrackEloProgressionCommand(IEloProgressionManager eloProgressionManager)
    {
        _eloProgressionManager = eloProgressionManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => false;
    public override string HelpMessageKey => "untrack_elo_progression_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target?.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts == null || parts.Length != 2
            || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            ReplyLocalizedHelpMessage(context);
            return Task.CompletedTask;
        }

        var format = parts[0].ToLowerAlphaNum();
        var userId = parts[1].ToLowerAlphaNum();

        if (string.IsNullOrWhiteSpace(format) || string.IsNullOrWhiteSpace(userId))
        {
            ReplyLocalizedHelpMessage(context);
            return Task.CompletedTask;
        }

        if (_eloProgressionManager.UntrackUser(format, userId))
        {
            context.ReplyLocalizedMessage("untrack_elo_progression_success", parts[1], parts[0]);
        }
        else
        {
            context.ReplyLocalizedMessage("untrack_elo_progression_not_found", parts[1], parts[0]);
        }

        return Task.CompletedTask;
    }
}
