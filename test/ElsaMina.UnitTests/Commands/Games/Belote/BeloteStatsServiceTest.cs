using ElsaMina.Commands.Games.Belote;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Belote;

public class BeloteStatsServiceTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private BeloteStatsService _sut;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _sut = new BeloteStatsService(_dbContextFactory);
    }

    private static BelotePlayer Player(string userId, int team, bool isTaker = false)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(userId);
        return new BelotePlayer(user) { Team = team, IsTaker = isTaker };
    }

    private static BelotePlayer[] FourPlayers(bool takerOnTeam0 = true) =>
    [
        Player("p0", 0, isTaker: takerOnTeam0),
        Player("p1", 1, isTaker: !takerOnTeam0),
        Player("p2", 0),
        Player("p3", 1)
    ];

    [Test]
    public async Task Test_RecordDealAsync_ShouldRecordTeamScoresAndWins_WhenTakerTeamWins()
    {
        var result = new BeloteScoreResult
        {
            TakerTeam = 0,
            Team0Score = 100,
            Team1Score = 62,
            Made = true,
            Deltas = [100, 62, 100, 62]
        };

        await _sut.RecordDealAsync(FourPlayers(), result);

        await using var dbContext = new BotDbContext(_dbOptions);
        var taker = await dbContext.BeloteStats.FindAsync("p0");
        var takerPartner = await dbContext.BeloteStats.FindAsync("p2");
        var defender = await dbContext.BeloteStats.FindAsync("p1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(taker.TotalScore, Is.EqualTo(100));
            Assert.That(taker.Wins, Is.EqualTo(1));
            Assert.That(taker.TimesTaker, Is.EqualTo(1));
            Assert.That(taker.TakerWins, Is.EqualTo(1));
            Assert.That(takerPartner.TotalScore, Is.EqualTo(100));
            Assert.That(takerPartner.Wins, Is.EqualTo(1));
            Assert.That(takerPartner.TimesTaker, Is.EqualTo(0));
            Assert.That(defender.TotalScore, Is.EqualTo(62));
            Assert.That(defender.Wins, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Test_RecordDealAsync_ShouldCountTakerButNotTakerWin_WhenContractFailed()
    {
        var result = new BeloteScoreResult
        {
            TakerTeam = 0,
            Team0Score = 0,
            Team1Score = 162,
            Made = false,
            Deltas = [0, 162, 0, 162]
        };

        await _sut.RecordDealAsync(FourPlayers(), result);

        await using var dbContext = new BotDbContext(_dbOptions);
        var taker = await dbContext.BeloteStats.FindAsync("p0");
        var defender = await dbContext.BeloteStats.FindAsync("p1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(taker.TimesTaker, Is.EqualTo(1));
            Assert.That(taker.TakerWins, Is.EqualTo(0));
            Assert.That(taker.Wins, Is.EqualTo(0));
            Assert.That(defender.Wins, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_RecordDealAsync_ShouldAccumulateOntoExistingStats()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.BeloteStats.Add(new BeloteStats
            {
                UserId = "p0",
                TotalScore = 200,
                GamesPlayed = 2,
                Wins = 1,
                TimesTaker = 1,
                TakerWins = 1
            });
            await setupContext.SaveChangesAsync();
        }

        var result = new BeloteScoreResult
        {
            TakerTeam = 0,
            Team0Score = 100,
            Team1Score = 62,
            Made = true,
            Deltas = [100, 62, 100, 62]
        };

        await _sut.RecordDealAsync(FourPlayers(), result);

        await using var dbContext = new BotDbContext(_dbOptions);
        var taker = await dbContext.BeloteStats.FindAsync("p0");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(taker.TotalScore, Is.EqualTo(300));
            Assert.That(taker.GamesPlayed, Is.EqualTo(3));
            Assert.That(taker.Wins, Is.EqualTo(2));
            Assert.That(taker.TimesTaker, Is.EqualTo(2));
            Assert.That(taker.TakerWins, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Test_RecordDealAsync_ShouldDoNothing_WhenDeltasDoNotMatchPlayerCount()
    {
        var result = new BeloteScoreResult { Made = true, Deltas = [100] };

        await _sut.RecordDealAsync(FourPlayers(), result);

        await using var dbContext = new BotDbContext(_dbOptions);
        Assert.That(await dbContext.BeloteStats.CountAsync(), Is.EqualTo(0));
    }
}