using ElsaMina.Commands.Showdown.Ladder.EloHistory;
using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Showdown.Ladder.EloHistory;

public class EloHistoryServiceTest
{
    private static readonly TimeSpan FAST_POLL = TimeSpan.FromMilliseconds(25);

    private IEloProgressionManager _eloProgressionManager;
    private IShowdownRanksProvider _showdownRanksProvider;
    private IBotDbContextFactory _dbContextFactory;
    private DbContextOptions<BotDbContext> _dbOptions;
    private EloHistoryService _service;

    [SetUp]
    public void SetUp()
    {
        _eloProgressionManager = Substitute.For<IEloProgressionManager>();
        _showdownRanksProvider = Substitute.For<IShowdownRanksProvider>();

        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new BotDbContext(_dbOptions);
        dbContext.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _service = new EloHistoryService(
            _eloProgressionManager,
            _showdownRanksProvider,
            _dbContextFactory,
            FAST_POLL);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public async Task Test_Start_ShouldNotCallRanksProvider_WhenNoUsersAreTracked()
    {
        // Arrange
        _eloProgressionManager.GetAllTrackedUsers().Returns([]);

        // Act
        _service.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Assert
        await _showdownRanksProvider.DidNotReceive()
            .GetRankingDataAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_Start_ShouldCallRanksProvider_ForEachTrackedUser()
    {
        // Arrange
        _eloProgressionManager.GetAllTrackedUsers().Returns([
            new EloTrackedUser("gen9ou", "alice"),
            new EloTrackedUser("gen8ou", "bob")
        ]);
        _showdownRanksProvider.GetRankingDataAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        _service.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Assert
        await _showdownRanksProvider.Received().GetRankingDataAsync("alice", Arg.Any<CancellationToken>());
        await _showdownRanksProvider.Received().GetRankingDataAsync("bob", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_Start_ShouldSaveSnapshot_WhenMatchingFormatEntryFound()
    {
        // Arrange
        _eloProgressionManager.GetAllTrackedUsers().Returns([
            new EloTrackedUser("gen9ou", "alice")
        ]);
        _showdownRanksProvider.GetRankingDataAsync("alice", Arg.Any<CancellationToken>())
            .Returns([
                new RankingDataDto { FormatId = "gen9ou", Elo = 1523.7 },
                new RankingDataDto { FormatId = "gen8ou", Elo = 1400.0 }
            ]);

        // Act
        _service.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Assert
        await using var dbContext = new BotDbContext(_dbOptions);
        var snapshots = await dbContext.LadderEloSnapshots.ToListAsync();
        Assert.That(snapshots, Has.Count.GreaterThanOrEqualTo(1));
        var snapshot = snapshots.First();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.UserId, Is.EqualTo("alice"));
            Assert.That(snapshot.Format, Is.EqualTo("gen9ou"));
            Assert.That(snapshot.Elo, Is.EqualTo(1524));
        }
    }

    [Test]
    public async Task Test_Start_ShouldNotSaveSnapshot_WhenFormatNotInUserRankings()
    {
        // Arrange
        _eloProgressionManager.GetAllTrackedUsers().Returns([
            new EloTrackedUser("gen9ou", "alice")
        ]);
        _showdownRanksProvider.GetRankingDataAsync("alice", Arg.Any<CancellationToken>())
            .Returns([
                new RankingDataDto { FormatId = "gen8ou", Elo = 1400.0 }
            ]);

        // Act
        _service.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Assert
        await using var dbContext = new BotDbContext(_dbOptions);
        var snapshots = await dbContext.LadderEloSnapshots.ToListAsync();
        Assert.That(snapshots, Is.Empty);
    }

    [Test]
    public async Task Test_Start_ShouldNotSaveSnapshot_WhenRankingsIsNull()
    {
        // Arrange
        _eloProgressionManager.GetAllTrackedUsers().Returns([
            new EloTrackedUser("gen9ou", "alice")
        ]);
        _showdownRanksProvider.GetRankingDataAsync("alice", Arg.Any<CancellationToken>())
            .Returns((IEnumerable<RankingDataDto>)null);

        // Act
        _service.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Assert
        await using var dbContext = new BotDbContext(_dbOptions);
        Assert.That(await dbContext.LadderEloSnapshots.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Test_Start_ShouldContinuePollingOtherUsers_WhenOneThrows()
    {
        // Arrange
        _eloProgressionManager.GetAllTrackedUsers().Returns([
            new EloTrackedUser("gen9ou", "alice"),
            new EloTrackedUser("gen9ou", "bob")
        ]);
        _showdownRanksProvider.GetRankingDataAsync("alice", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("network error"));
        _showdownRanksProvider.GetRankingDataAsync("bob", Arg.Any<CancellationToken>())
            .Returns([new RankingDataDto { FormatId = "gen9ou", Elo = 1300.0 }]);

        // Act
        _service.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Assert
        await using var dbContext = new BotDbContext(_dbOptions);
        var snapshots = await dbContext.LadderEloSnapshots.ToListAsync();
        Assert.That(snapshots.Any(s => s.UserId == "bob" && s.Format == "gen9ou"), Is.True);
    }

    [Test]
    public async Task Test_Dispose_ShouldStopPolling()
    {
        // Arrange
        _eloProgressionManager.GetAllTrackedUsers().Returns([]);

        // Act
        _service.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        _service.Dispose();
        var callCountAtDispose = _eloProgressionManager.ReceivedCalls().Count();
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.That(_eloProgressionManager.ReceivedCalls().Count(),
            Is.LessThanOrEqualTo(callCountAtDispose + 1));
    }
}
