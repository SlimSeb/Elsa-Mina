using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Tarot;

public class TarotStatsService : ITarotStatsService
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public TarotStatsService(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task RecordDealAsync(IReadOnlyList<TarotPlayer> players, TarotScoreResult result,
        CancellationToken cancellationToken = default)
    {
        if (players is null || result is null || result.Deltas.Length != players.Count)
        {
            return;
        }

        Log.Information("Recording tarot deal stats for {0} players", players.Count);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var delta = result.Deltas[i];
            var stats = await GetOrCreateStatsAsync(dbContext, player.UserId, cancellationToken);

            stats.TotalScoreHalfPoints += delta;
            stats.GamesPlayed++;
            if (delta > 0)
            {
                stats.Wins++;
            }

            if (player.IsTaker)
            {
                stats.TimesTaker++;
                if (result.Made)
                {
                    stats.TakerWins++;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<TarotStats> GetOrCreateStatsAsync(BotDbContext dbContext, string userId,
        CancellationToken cancellationToken)
    {
        var stats = await dbContext.TarotStats
            .FirstOrDefaultAsync(entry => entry.UserId == userId, cancellationToken);

        if (stats is not null)
        {
            return stats;
        }

        stats = new TarotStats { UserId = userId };
        dbContext.TarotStats.Add(stats);
        return stats;
    }
}
