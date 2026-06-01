using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Tarot;

[NamedCommand("tarotstats", Aliases = ["tarotscore"])]
public class TarotStatsCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public TarotStatsCommand(IBotDbContextFactory dbContextFactory)
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
        var stats = await dbContext.TarotStats
            .FirstOrDefaultAsync(entry => entry.UserId == userId, cancellationToken);

        if (stats is null)
        {
            context.ReplyLocalizedMessage("tarot_stats_not_found", userId);
            return;
        }

        context.ReplyLocalizedMessage("tarot_stats_info", stats.UserId, stats.TotalScoreHalfPoints / 2.0,
            stats.GamesPlayed, stats.Wins, stats.TimesTaker, stats.TakerWins);
    }
}
