using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Belote;

[NamedCommand("belotestats", Aliases = ["belotescore"])]
public class BeloteStatsCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public BeloteStatsCommand(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var userId = string.IsNullOrWhiteSpace(context.Target)
            ? context.Sender.UserId
            : context.Target.Trim().ToLowerInvariant().Replace(" ", "");

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var stats = await dbContext.BeloteStats
            .FirstOrDefaultAsync(entry => entry.UserId == userId, cancellationToken);

        if (stats is null)
        {
            context.ReplyLocalizedMessage("belote_stats_not_found", userId);
            return;
        }

        context.ReplyLocalizedMessage("belote_stats_info", stats.UserId, stats.TotalScore,
            stats.GamesPlayed, stats.Wins, stats.TimesTaker, stats.TakerWins);
    }
}
