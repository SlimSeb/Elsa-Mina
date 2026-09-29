using ElsaMina.Commands.Games.Battleship;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Battleship;

public class BattleshipRatingServiceTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private BattleshipRatingService _sut;
    private IUser _winner;
    private IUser _loser;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _winner = Substitute.For<IUser>();
        _winner.UserId.Returns("winner");
        _loser = Substitute.For<IUser>();
        _loser.UserId.Returns("loser");

        _sut = new BattleshipRatingService(_dbContextFactory);
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldCreateRatingsAndUsers_WhenUsersHaveNoExistingRating()
    {
        await _sut.UpdateRatingsOnWinAsync(_winner, _loser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var winnerRating = await dbContext.BattleshipRatings.FindAsync("winner");
        var loserRating = await dbContext.BattleshipRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerRating, Is.Not.Null);
            Assert.That(loserRating, Is.Not.Null);
            Assert.That(winnerRating.Wins, Is.EqualTo(1));
            Assert.That(loserRating.Losses, Is.EqualTo(1));
            Assert.That(await dbContext.Users.FindAsync("winner"), Is.Not.Null);
            Assert.That(await dbContext.Users.FindAsync("loser"), Is.Not.Null);
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldIncrementWinsAndLosses_WhenRatingsExist()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.BattleshipRatings.Add(new BattleshipRating
                { UserId = "winner", Rating = 1000, Wins = 2, Losses = 1 });
            setupContext.BattleshipRatings.Add(new BattleshipRating
                { UserId = "loser", Rating = 1000, Wins = 1, Losses = 2 });
            await setupContext.SaveChangesAsync();
        }

        await _sut.UpdateRatingsOnWinAsync(_winner, _loser);

        await using var dbContext = new BotDbContext(_dbOptions);
        var winnerRating = await dbContext.BattleshipRatings.FindAsync("winner");
        var loserRating = await dbContext.BattleshipRatings.FindAsync("loser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerRating.Wins, Is.EqualTo(3));
            Assert.That(winnerRating.Losses, Is.EqualTo(1));
            Assert.That(loserRating.Wins, Is.EqualTo(1));
            Assert.That(loserRating.Losses, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldReturnAndPersistEloChanges()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.BattleshipRatings.Add(new BattleshipRating { UserId = "winner", Rating = 1100 });
            setupContext.BattleshipRatings.Add(new BattleshipRating { UserId = "loser", Rating = 1200 });
            await setupContext.SaveChangesAsync();
        }

        var (expectedWinnerRating, expectedLoserRating) = EloHelper.CalculateWinRatings(1100, 1200);

        var (winnerChange, loserChange) = await _sut.UpdateRatingsOnWinAsync(_winner, _loser);

        await using var dbContext = new BotDbContext(_dbOptions);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerChange, Is.EqualTo(new BattleshipRatingChange(1100, expectedWinnerRating)));
            Assert.That(loserChange, Is.EqualTo(new BattleshipRatingChange(1200, expectedLoserRating)));
            Assert.That(winnerChange.Delta, Is.Positive);
            Assert.That(loserChange.Delta, Is.Negative);
            Assert.That((await dbContext.BattleshipRatings.FindAsync("winner")).Rating,
                Is.EqualTo(expectedWinnerRating));
            Assert.That((await dbContext.BattleshipRatings.FindAsync("loser")).Rating,
                Is.EqualTo(expectedLoserRating));
        }
    }

    [Test]
    public async Task Test_UpdateRatingsOnWinAsync_ShouldStartFromDefaultRating_WhenUsersHaveNoExistingRating()
    {
        var (winnerChange, loserChange) = await _sut.UpdateRatingsOnWinAsync(_winner, _loser);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winnerChange.OldRating, Is.EqualTo(EloHelper.DEFAULT_RATING));
            Assert.That(loserChange.OldRating, Is.EqualTo(EloHelper.DEFAULT_RATING));
        }
    }
}
