using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.Chess;

public class ChessRatingService : IChessRatingService
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public ChessRatingService(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<(ChessRatingChange, ChessRatingChange)> UpdateRatingsOnWinAsync(IUser winner, IUser loser,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Updating chess ratings on win for {0} vs. {1}", winner, loser);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var winnerRating = await GetOrCreateRatingAsync(dbContext, winner.UserId, cancellationToken);
        var loserRating = await GetOrCreateRatingAsync(dbContext, loser.UserId, cancellationToken);

        var (newWinnerRating, newLoserRating) = EloHelper.CalculateWinRatings(winnerRating.Rating, loserRating.Rating);

        var winnerChange = new ChessRatingChange(winnerRating.Rating, newWinnerRating);
        var loserChange = new ChessRatingChange(loserRating.Rating, newLoserRating);

        winnerRating.Rating = newWinnerRating;
        winnerRating.Wins++;

        loserRating.Rating = newLoserRating;
        loserRating.Losses++;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (winnerChange, loserChange);
    }

    public async Task<(ChessRatingChange, ChessRatingChange)> UpdateRatingsOnDrawAsync(IUser player1, IUser player2,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Updating chess ratings on draw for {0} and {1}", player1, player2);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rating1 = await GetOrCreateRatingAsync(dbContext, player1.UserId, cancellationToken);
        var rating2 = await GetOrCreateRatingAsync(dbContext, player2.UserId, cancellationToken);

        var (newRating1, newRating2) = EloHelper.CalculateDrawRatings(rating1.Rating, rating2.Rating);

        var change1 = new ChessRatingChange(rating1.Rating, newRating1);
        var change2 = new ChessRatingChange(rating2.Rating, newRating2);

        rating1.Rating = newRating1;
        rating1.Draws++;

        rating2.Rating = newRating2;
        rating2.Draws++;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (change1, change2);
    }

    private static async Task<ChessRating> GetOrCreateRatingAsync(BotDbContext dbContext, string userId,
        CancellationToken cancellationToken)
    {
        var rating = await dbContext.ChessRatings
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (rating is not null)
        {
            return rating;
        }

        await dbContext.EnsureUserExistsAsync(userId, cancellationToken);
        rating = new ChessRating
        {
            UserId = userId,
            Rating = EloHelper.DEFAULT_RATING
        };
        dbContext.ChessRatings.Add(rating);
        return rating;
    }
}
