using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Showdown.Ladder.EloHistory;

[NamedCommand("trackeloprogression", "trackelo", "elotrack")]
public class TrackEloProgressionCommand : Command
{
    private readonly IEloProgressionManager _eloProgressionManager;

    public TrackEloProgressionCommand(IEloProgressionManager eloProgressionManager)
    {
        _eloProgressionManager = eloProgressionManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => false;
    public override string HelpMessageKey => "track_elo_progression_help";

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

        if (_eloProgressionManager.TrackUser(format, userId))
        {
            context.ReplyLocalizedMessage("track_elo_progression_success", parts[1], parts[0]);
        }
        else
        {
            context.ReplyLocalizedMessage("track_elo_progression_already_tracked", parts[1], parts[0]);
        }

        return Task.CompletedTask;
    }
}
