using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Belote;

public class BeloteStatsService : IBeloteStatsService
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public BeloteStatsService(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task RecordDealAsync(IReadOnlyList<BelotePlayer> players, BeloteScoreResult result,
        CancellationToken cancellationToken = default)
    {
        if (players is null || result is null || result.Deltas.Length != players.Count)
        {
            return;
        }

        Log.Information("Recording belote deal stats for {0} players", players.Count);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var teamScore = player.Team == 0 ? result.Team0Score : result.Team1Score;
            var opponentScore = player.Team == 0 ? result.Team1Score : result.Team0Score;
            var stats = await GetOrCreateStatsAsync(dbContext, player.UserId, cancellationToken);

            stats.TotalScore += teamScore;
            stats.GamesPlayed++;
            if (teamScore > opponentScore)
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

    private static async Task<BeloteStats> GetOrCreateStatsAsync(BotDbContext dbContext, string userId,
        CancellationToken cancellationToken)
    {
        var stats = await dbContext.BeloteStats
            .FirstOrDefaultAsync(entry => entry.UserId == userId, cancellationToken);

        if (stats is not null)
        {
            return stats;
        }

        await dbContext.EnsureUserExistsAsync(userId, cancellationToken);
        stats = new BeloteStats { UserId = userId };
        dbContext.BeloteStats.Add(stats);
        return stats;
    }
}
