using ElsaMina.Commands.Games.Chess;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Chess;

public class ChessRatingServiceTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ChessRatingService _sut;
    private IUser _mockWinner;
    private IUser _mockLoser;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => new BotDbContext(_dbOptions));

        _mockWinner = Substitute.For<IUser>();
        _mockWinner.UserId.Returns("winner");
        _mockLoser = Substitute.For<IUser>();
        _mockLoser.UserId.Returns("loser");

        _sut = new ChessRatingService(_dbContextFactory);
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldCreateRatings_WhenUsersHaveNoExistingRating()
    {
        await _sut.UpdateRatingsOnWinAsync(_mockWinner, _mockLoser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var winnerRating = await dbContext.ChessRatings.FindAsync("winner");
        var loserRating = await dbContext.ChessRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerRating, Is.Not.Null);
            Assert.That(loserRating, Is.Not.Null);
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldIncrementWinsAndLosses()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.ChessRatings.Add(new ChessRating { UserId = "winner", Rating = 1000, Wins = 2, Losses = 1 });
            setupContext.ChessRatings.Add(new ChessRating { UserId = "loser", Rating = 1000, Wins = 1, Losses = 2 });
            await setupContext.SaveChangesAsync();
        }

        await _sut.UpdateRatingsOnWinAsync(_mockWinner, _mockLoser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var winnerRating = await dbContext.ChessRatings.FindAsync("winner");
        var loserRating = await dbContext.ChessRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerRating.Wins, Is.EqualTo(3));
            Assert.That(loserRating.Losses, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldReturnCorrectChanges()
    {
        var (expectedWinner, expectedLoser) =
            EloHelper.CalculateWinRatings(EloHelper.DEFAULT_RATING, EloHelper.DEFAULT_RATING);

        var (winnerChange, loserChange) = await _sut.UpdateRatingsOnWinAsync(_mockWinner, _mockLoser);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerChange.NewRating, Is.EqualTo(expectedWinner));
            Assert.That(loserChange.NewRating, Is.EqualTo(expectedLoser));
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnDrawAsync_ShouldIncrementDraws()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.ChessRatings.Add(new ChessRating { UserId = "winner", Rating = 1000, Draws = 1 });
            setupContext.ChessRatings.Add(new ChessRating { UserId = "loser", Rating = 1000, Draws = 2 });
            await setupContext.SaveChangesAsync();
        }

        await _sut.UpdateRatingsOnDrawAsync(_mockWinner, _mockLoser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var rating1 = await dbContext.ChessRatings.FindAsync("winner");
        var rating2 = await dbContext.ChessRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rating1.Draws, Is.EqualTo(2));
            Assert.That(rating2.Draws, Is.EqualTo(3));
        }
    }
}
