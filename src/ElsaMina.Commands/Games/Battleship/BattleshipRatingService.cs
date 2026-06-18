using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Battleship;

public class BattleshipRatingService : IBattleshipRatingService
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public BattleshipRatingService(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<(BattleshipRatingChange, BattleshipRatingChange)> UpdateRatingsOnWinAsync(IUser winner,
        IUser loser, CancellationToken cancellationToken = default)
    {
        Log.Information("Updating battleship ratings on win for {0} vs. {1}", winner, loser);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var winnerRating = await GetOrCreateRatingAsync(dbContext, winner.UserId, cancellationToken);
        var loserRating = await GetOrCreateRatingAsync(dbContext, loser.UserId, cancellationToken);

        var (newWinnerRating, newLoserRating) = EloHelper.CalculateWinRatings(winnerRating.Rating, loserRating.Rating);

        var winnerChange = new BattleshipRatingChange(winnerRating.Rating, newWinnerRating);
        var loserChange = new BattleshipRatingChange(loserRating.Rating, newLoserRating);

        winnerRating.Rating = newWinnerRating;
        winnerRating.Wins++;

        loserRating.Rating = newLoserRating;
        loserRating.Losses++;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (winnerChange, loserChange);
    }

    private static async Task<BattleshipRating> GetOrCreateRatingAsync(BotDbContext dbContext, string userId,
        CancellationToken cancellationToken)
    {
        var rating = await dbContext.BattleshipRatings
            .FirstOrDefaultAsync(entry => entry.UserId == userId, cancellationToken);

        if (rating is not null)
        {
            return rating;
        }

        await dbContext.EnsureUserExistsAsync(userId, cancellationToken);
        rating = new BattleshipRating
        {
            UserId = userId,
            Rating = EloHelper.DEFAULT_RATING
        };
        dbContext.BattleshipRatings.Add(rating);
        return rating;
    }
}
