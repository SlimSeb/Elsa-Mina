using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

public class TarotStatsServiceTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private TarotStatsService _sut;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _sut = new TarotStatsService(_dbContextFactory);
    }

    private static TarotPlayer Player(string userId, bool isTaker = false)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(userId);
        return new TarotPlayer(user) { IsTaker = isTaker };
    }

    [Test]
    public async Task Test_RecordDealAsync_ShouldCreateStatsForEachPlayer_WhenNoneExist()
    {
        var players = new[] { Player("taker", isTaker: true), Player("defender") };
        var result = new TarotScoreResult { Made = true, Deltas = [40, -40] };

        await _sut.RecordDealAsync(players, result);

        await using var dbContext = new BotDbContext(_dbOptions);
        var taker = await dbContext.TarotStats.FindAsync("taker");
        var defender = await dbContext.TarotStats.FindAsync("defender");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(taker, Is.Not.Null);
            Assert.That(defender, Is.Not.Null);
            Assert.That(taker.TotalScoreHalfPoints, Is.EqualTo(40));
            Assert.That(defender.TotalScoreHalfPoints, Is.EqualTo(-40));
        }
    }

    [Test]
    public async Task Test_RecordDealAsync_ShouldCountWinsForPositiveDeltasOnly()
    {
        var players = new[] { Player("taker", isTaker: true), Player("defender") };
        var result = new TarotScoreResult { Made = true, Deltas = [40, -40] };

        await _sut.RecordDealAsync(players, result);

        await using var dbContext = new BotDbContext(_dbOptions);
        var taker = await dbContext.TarotStats.FindAsync("taker");
        var defender = await dbContext.TarotStats.FindAsync("defender");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(taker.Wins, Is.EqualTo(1));
            Assert.That(defender.Wins, Is.EqualTo(0));
            Assert.That(taker.GamesPlayed, Is.EqualTo(1));
            Assert.That(defender.GamesPlayed, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_RecordDealAsync_ShouldCountTakerAndTakerWins_WhenContractMade()
    {
        var players = new[] { Player("taker", isTaker: true), Player("defender") };
        var result = new TarotScoreResult { Made = true, Deltas = [40, -40] };

        await _sut.RecordDealAsync(players, result);

        await using var dbContext = new BotDbContext(_dbOptions);
        var taker = await dbContext.TarotStats.FindAsync("taker");
        var defender = await dbContext.TarotStats.FindAsync("defender");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(taker.TimesTaker, Is.EqualTo(1));
            Assert.That(taker.TakerWins, Is.EqualTo(1));
            Assert.That(defender.TimesTaker, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Test_RecordDealAsync_ShouldCountTakerButNotTakerWin_WhenContractFailed()
    {
        var players = new[] { Player("taker", isTaker: true), Player("defender") };
        var result = new TarotScoreResult { Made = false, Deltas = [-40, 40] };

        await _sut.RecordDealAsync(players, result);

        await using var dbContext = new BotDbContext(_dbOptions);
        var taker = await dbContext.TarotStats.FindAsync("taker");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(taker.TimesTaker, Is.EqualTo(1));
            Assert.That(taker.TakerWins, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Test_RecordDealAsync_ShouldAccumulateOntoExistingStats()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.TarotStats.Add(new TarotStats
            {
                UserId = "taker",
                TotalScoreHalfPoints = 100,
                GamesPlayed = 2,
                Wins = 1,
                TimesTaker = 1,
                TakerWins = 1
            });
            await setupContext.SaveChangesAsync();
        }

        var players = new[] { Player("taker", isTaker: true), Player("defender") };
        var result = new TarotScoreResult { Made = true, Deltas = [40, -40] };

        await _sut.RecordDealAsync(players, result);

        await using var dbContext = new BotDbContext(_dbOptions);
        var taker = await dbContext.TarotStats.FindAsync("taker");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(taker.TotalScoreHalfPoints, Is.EqualTo(140));
            Assert.That(taker.GamesPlayed, Is.EqualTo(3));
            Assert.That(taker.Wins, Is.EqualTo(2));
            Assert.That(taker.TimesTaker, Is.EqualTo(2));
            Assert.That(taker.TakerWins, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Test_RecordDealAsync_ShouldDoNothing_WhenDeltasDoNotMatchPlayerCount()
    {
        var players = new[] { Player("taker", isTaker: true), Player("defender") };
        var result = new TarotScoreResult { Made = true, Deltas = [40] };

        await _sut.RecordDealAsync(players, result);

        await using var dbContext = new BotDbContext(_dbOptions);
        Assert.That(await dbContext.TarotStats.CountAsync(), Is.EqualTo(0));
    }
}
