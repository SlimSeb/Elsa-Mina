using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Games.ConnectFour;

public class ConnectFourRatingService : IConnectFourRatingService
{
    private readonly IBotDbContextFactory _dbContextFactory;

    public ConnectFourRatingService(IBotDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<(ConnectFourRatingChange, ConnectFourRatingChange)> UpdateRatingsOnWinAsync(IUser winner, IUser loser, CancellationToken cancellationToken = default)
    {
        Log.Information("Updating ratings on win for {0} vs. {1}", winner, loser);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var winnerRating = await GetOrCreateRatingAsync(dbContext, winner.UserId, cancellationToken);
        var loserRating = await GetOrCreateRatingAsync(dbContext, loser.UserId, cancellationToken);

        var (newWinnerRating, newLoserRating) = EloHelper.CalculateWinRatings(winnerRating.Rating, loserRating.Rating);

        var winnerChange = new ConnectFourRatingChange(winnerRating.Rating, newWinnerRating);
        var loserChange = new ConnectFourRatingChange(loserRating.Rating, newLoserRating);

        winnerRating.Rating = newWinnerRating;
        winnerRating.Wins++;

        loserRating.Rating = newLoserRating;
        loserRating.Losses++;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (winnerChange, loserChange);
    }

    public async Task<(ConnectFourRatingChange, ConnectFourRatingChange)> UpdateRatingsOnDrawAsync(IUser player1, IUser player2, CancellationToken cancellationToken = default)
    {
        Log.Information("Updating ratings on draw for {0} and {1}", player1, player2);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rating1 = await GetOrCreateRatingAsync(dbContext, player1.UserId, cancellationToken);
        var rating2 = await GetOrCreateRatingAsync(dbContext, player2.UserId, cancellationToken);

        var (newRating1, newRating2) = EloHelper.CalculateDrawRatings(rating1.Rating, rating2.Rating);

        var change1 = new ConnectFourRatingChange(rating1.Rating, newRating1);
        var change2 = new ConnectFourRatingChange(rating2.Rating, newRating2);

        rating1.Rating = newRating1;
        rating1.Draws++;

        rating2.Rating = newRating2;
        rating2.Draws++;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (change1, change2);
    }

    private static async Task<ConnectFourRating> GetOrCreateRatingAsync(BotDbContext dbContext, string userId,
        CancellationToken cancellationToken)
    {
        var rating = await dbContext.ConnectFourRatings
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (rating is not null)
        {
            return rating;
        }

        rating = new ConnectFourRating
        {
            UserId = userId,
            Rating = EloHelper.DEFAULT_RATING
        };
        dbContext.ConnectFourRatings.Add(rating);
        return rating;
    }
}
