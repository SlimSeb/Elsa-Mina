using System.Text;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Showdown.Ladder.EloHistory;

[NamedCommand("listelo", "trackedelo", "elolist", "eloprogression")]
public class ListTrackedEloProgressionsCommand : Command
{
    private readonly IEloProgressionManager _eloProgressionManager;

    public ListTrackedEloProgressionsCommand(IEloProgressionManager eloProgressionManager)
    {
        _eloProgressionManager = eloProgressionManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => false;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var trackedUsers = _eloProgressionManager.GetAllTrackedUsers();
        if (trackedUsers.Count == 0)
        {
            context.ReplyLocalizedMessage("list_elo_progressions_none");
            return Task.CompletedTask;
        }

        var builder = new StringBuilder();
        foreach (var user in trackedUsers.OrderBy(u => u.Format).ThenBy(u => u.UserId))
        {
            builder.AppendLine(context.GetString("list_elo_progressions_entry", user.UserId, user.Format));
        }

        context.ReplyHtml(builder.ToString());
        return Task.CompletedTask;
    }
}
