using ElsaMina.Commands.Games.ConnectFour;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.ConnectFour;

public class ConnectFourRatingServiceTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ConnectFourRatingService _sut;
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

        _sut = new ConnectFourRatingService(_dbContextFactory);
    }

    #region UpdateRatingsOnWinAsync

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldCreateRatings_WhenUsersHaveNoExistingRating()
    {
        await _sut.UpdateRatingsOnWinAsync(_mockWinner, _mockLoser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var winnerRating = await dbContext.ConnectFourRatings.FindAsync("winner");
        var loserRating = await dbContext.ConnectFourRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerRating, Is.Not.Null);
            Assert.That(loserRating, Is.Not.Null);
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldUseDefaultRating_WhenCreatingNewRatings()
    {
        await _sut.UpdateRatingsOnWinAsync(_mockWinner, _mockLoser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var (expectedWinner, expectedLoser) = EloHelper.CalculateWinRatings(EloHelper.DEFAULT_RATING, EloHelper.DEFAULT_RATING);
        var winnerRating = await dbContext.ConnectFourRatings.FindAsync("winner");
        var loserRating = await dbContext.ConnectFourRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerRating.Rating, Is.EqualTo(expectedWinner));
            Assert.That(loserRating.Rating, Is.EqualTo(expectedLoser));
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldUpdateRatings_WhenUsersHaveExistingRatings()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "winner", Rating = 1100 });
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "loser", Rating = 900 });
            await setupContext.SaveChangesAsync();
        }

        await _sut.UpdateRatingsOnWinAsync(_mockWinner, _mockLoser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var (expectedWinner, expectedLoser) = EloHelper.CalculateWinRatings(1100, 900);
        var winnerRating = await dbContext.ConnectFourRatings.FindAsync("winner");
        var loserRating = await dbContext.ConnectFourRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerRating.Rating, Is.EqualTo(expectedWinner));
            Assert.That(loserRating.Rating, Is.EqualTo(expectedLoser));
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldIncrementWinsAndLosses()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "winner", Rating = 1000, Wins = 2, Losses = 1 });
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "loser", Rating = 1000, Wins = 1, Losses = 2 });
            await setupContext.SaveChangesAsync();
        }

        await _sut.UpdateRatingsOnWinAsync(_mockWinner, _mockLoser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var winnerRating = await dbContext.ConnectFourRatings.FindAsync("winner");
        var loserRating = await dbContext.ConnectFourRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerRating.Wins, Is.EqualTo(3));
            Assert.That(winnerRating.Losses, Is.EqualTo(1));
            Assert.That(loserRating.Losses, Is.EqualTo(3));
            Assert.That(loserRating.Wins, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldReturnCorrectChanges()
    {
        var (expectedWinner, expectedLoser) = EloHelper.CalculateWinRatings(EloHelper.DEFAULT_RATING, EloHelper.DEFAULT_RATING);

        var (winnerChange, loserChange) = await _sut.UpdateRatingsOnWinAsync(_mockWinner, _mockLoser);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerChange.OldRating, Is.EqualTo(EloHelper.DEFAULT_RATING));
            Assert.That(winnerChange.NewRating, Is.EqualTo(expectedWinner));
            Assert.That(winnerChange.Delta, Is.EqualTo(expectedWinner - EloHelper.DEFAULT_RATING));
            Assert.That(loserChange.OldRating, Is.EqualTo(EloHelper.DEFAULT_RATING));
            Assert.That(loserChange.NewRating, Is.EqualTo(expectedLoser));
            Assert.That(loserChange.Delta, Is.EqualTo(expectedLoser - EloHelper.DEFAULT_RATING));
        }
    }

    #endregion

    #region UpdateRatingsOnDrawAsync

    [Test]
    public async Task Test_UpdateRatingsOnDrawAsync_ShouldCreateRatings_WhenUsersHaveNoExistingRating()
    {
        await _sut.UpdateRatingsOnDrawAsync(_mockWinner, _mockLoser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var rating1 = await dbContext.ConnectFourRatings.FindAsync("winner");
        var rating2 = await dbContext.ConnectFourRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rating1, Is.Not.Null);
            Assert.That(rating2, Is.Not.Null);
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnDrawAsync_ShouldIncrementDraws()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "winner", Rating = 1000, Draws = 1 });
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "loser", Rating = 1000, Draws = 2 });
            await setupContext.SaveChangesAsync();
        }

        await _sut.UpdateRatingsOnDrawAsync(_mockWinner, _mockLoser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var rating1 = await dbContext.ConnectFourRatings.FindAsync("winner");
        var rating2 = await dbContext.ConnectFourRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rating1.Draws, Is.EqualTo(2));
            Assert.That(rating2.Draws, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnDrawAsync_ShouldReturnCorrectChanges()
    {
        var (expectedRating1, expectedRating2) = EloHelper.CalculateDrawRatings(EloHelper.DEFAULT_RATING, EloHelper.DEFAULT_RATING);

        var (change1, change2) = await _sut.UpdateRatingsOnDrawAsync(_mockWinner, _mockLoser);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(change1.OldRating, Is.EqualTo(EloHelper.DEFAULT_RATING));
            Assert.That(change1.NewRating, Is.EqualTo(expectedRating1));
            Assert.That(change1.Delta, Is.EqualTo(expectedRating1 - EloHelper.DEFAULT_RATING));
            Assert.That(change2.OldRating, Is.EqualTo(EloHelper.DEFAULT_RATING));
            Assert.That(change2.NewRating, Is.EqualTo(expectedRating2));
            Assert.That(change2.Delta, Is.EqualTo(expectedRating2 - EloHelper.DEFAULT_RATING));
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnDrawAsync_ShouldNotIncrementWinsOrLosses()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "winner", Rating = 1000, Wins = 3, Losses = 2 });
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "loser", Rating = 1000, Wins = 1, Losses = 4 });
            await setupContext.SaveChangesAsync();
        }

        await _sut.UpdateRatingsOnDrawAsync(_mockWinner, _mockLoser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var rating1 = await dbContext.ConnectFourRatings.FindAsync("winner");
        var rating2 = await dbContext.ConnectFourRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rating1.Wins, Is.EqualTo(3));
            Assert.That(rating1.Losses, Is.EqualTo(2));
            Assert.That(rating2.Wins, Is.EqualTo(1));
            Assert.That(rating2.Losses, Is.EqualTo(4));
        }
    }

    #endregion
}
