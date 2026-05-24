using ElsaMina.Commands.Profile;
using ElsaMina.Commands.Tournaments.Handlers;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Tournaments.Handlers;

public class TourEndHandlerTest
{
    private const string TOUR_JSON =
        """{"results":[["Pujolly"]],"format":"Random Inverse Party #2","generator":"Single Elimination","bracketData":{"type":"tree","rootNode":{"children":[{"children":[{"children":[{"children":[{"team":"Emon123"},{"team":"Drafeu-kun"}],"state":"finished","team":"Emon123","result":"win","score":[3,0]},{"team":"palapapop"}],"state":"finished","team":"Emon123","result":"win","score":[1,0]},{"children":[{"team":"Reegychodon_64"},{"team":"Dragonillis"}],"state":"finished","team":"Reegychodon_64","result":"win","score":[1,0]}],"state":"finished","team":"Emon123","result":"win","score":[3,0]},{"children":[{"children":[{"team":"Naiike"},{"team":"Pujolly"}],"state":"finished","team":"Pujolly","result":"loss","score":[5,6]},{"children":[{"team":"le ru c'est la rue"},{"team":"Bloody jae"}],"state":"finished","team":"Bloody jae","result":"loss","score":[0,2]}],"state":"finished","team":"Pujolly","result":"win","score":[6,1]}],"state":"finished","team":"Pujolly","result":"loss","score":[2,2]}}}""";

    private IBotDbContextFactory _botDbContextFactory;
    private IRoomUserDataService _roomUserDataService;
    private IProfileService _profileService;
    private IBot _bot;
    private IClockService _clockService;
    private DbContextOptions<BotDbContext> _dbOptions;
    private TourEndHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _botDbContextFactory = Substitute.For<IBotDbContextFactory>();
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _profileService = Substitute.For<IProfileService>();
        _bot = Substitute.For<IBot>();
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _botDbContextFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));
        _profileService
            .GetProfileHtmlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("<profile/>");

        _handler = new TourEndHandler(_botDbContextFactory, _roomUserDataService, _profileService, _bot, _clockService);
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.Database.EnsureDeletedAsync();
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotAccessDb_WhenPartsAreTooShort()
    {
        var parts = new[] { "", "tournament", "end" };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await _botDbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotAccessDb_WhenMessageTypeIsNotTournamentEnd()
    {
        var parts = new[] { "", "tournament", "update", TOUR_JSON };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await _botDbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotAccessDb_WhenTournamentIsNotSingleElimination()
    {
        var doubleElimJson = TOUR_JSON.Replace("Single Elimination", "Double Elimination");
        var parts = new[] { "", "tournament", "end", doubleElimJson };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await _botDbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldCreateRecordsForAllNinePlayers_WhenValidData()
    {
        var parts = new[] { "", "tournament", "end", TOUR_JSON };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await using var dbContext = new BotDbContext(_dbOptions);
        Assert.That(await dbContext.TournamentRecords.CountAsync(), Is.EqualTo(9));
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAssignRoomIdToAllRecords_WhenValidData()
    {
        var parts = new[] { "", "tournament", "end", TOUR_JSON };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await using var dbContext = new BotDbContext(_dbOptions);
        var records = await dbContext.TournamentRecords.ToListAsync();
        Assert.That(records.All(record => record.RoomId == "arcade"), Is.True);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIncrementTournamentsEnteredCount_ForEveryPlayer()
    {
        var parts = new[] { "", "tournament", "end", TOUR_JSON };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await using var dbContext = new BotDbContext(_dbOptions);
        var records = await dbContext.TournamentRecords.ToListAsync();
        Assert.That(records.All(record => record.TournamentsEnteredCount == 1), Is.True);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSetCorrectStats_ForWinner()
    {
        // Pujolly: winner - 3 match wins, 0 losses (no loss as winner in single elim)
        var parts = new[] { "", "tournament", "end", TOUR_JSON };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await using var dbContext = new BotDbContext(_dbOptions);
        var record = await dbContext.TournamentRecords.FindAsync(["pujolly", "arcade"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.WinsCount, Is.EqualTo(1));
            Assert.That(record.RunnerUpCount, Is.EqualTo(0));
            Assert.That(record.ThirdPlaceCount, Is.EqualTo(0));
            Assert.That(record.WonGames, Is.EqualTo(3));
            Assert.That(record.PlayedGames, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSetCorrectStats_ForRunnerUp()
    {
        // Emon123: runner-up - 3 match wins + 1 final loss
        var parts = new[] { "", "tournament", "end", TOUR_JSON };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await using var dbContext = new BotDbContext(_dbOptions);
        var record = await dbContext.TournamentRecords.FindAsync(["emon123", "arcade"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.WinsCount, Is.EqualTo(0));
            Assert.That(record.RunnerUpCount, Is.EqualTo(1));
            Assert.That(record.ThirdPlaceCount, Is.EqualTo(0));
            Assert.That(record.WonGames, Is.EqualTo(3));
            Assert.That(record.PlayedGames, Is.EqualTo(4));
        }
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSetCorrectStats_ForSemiFinalists()
    {
        // Reegychodon_64 and Bloody jae: semi-finalists - 1 match win + 1 loss each
        var parts = new[] { "", "tournament", "end", TOUR_JSON };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await using var dbContext = new BotDbContext(_dbOptions);
        var reegychodon = await dbContext.TournamentRecords.FindAsync(["reegychodon64", "arcade"]);
        var bloodyjae = await dbContext.TournamentRecords.FindAsync(["bloodyjae", "arcade"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reegychodon.ThirdPlaceCount, Is.EqualTo(1));
            Assert.That(reegychodon.WonGames, Is.EqualTo(1));
            Assert.That(reegychodon.PlayedGames, Is.EqualTo(2));
            Assert.That(bloodyjae.ThirdPlaceCount, Is.EqualTo(1));
            Assert.That(bloodyjae.WonGames, Is.EqualTo(1));
            Assert.That(bloodyjae.PlayedGames, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSetCorrectStats_ForFirstRoundLoser()
    {
        // Drafeu-kun: lost in round 1 - 0 wins, 1 game played
        var parts = new[] { "", "tournament", "end", TOUR_JSON };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await using var dbContext = new BotDbContext(_dbOptions);
        var record = await dbContext.TournamentRecords.FindAsync(["drafeukun", "arcade"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.WinsCount, Is.EqualTo(0));
            Assert.That(record.RunnerUpCount, Is.EqualTo(0));
            Assert.That(record.ThirdPlaceCount, Is.EqualTo(0));
            Assert.That(record.WonGames, Is.EqualTo(0));
            Assert.That(record.PlayedGames, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotSetWinsOrRunnerUp_ForNonFinalists()
    {
        var parts = new[] { "", "tournament", "end", TOUR_JSON };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await using var dbContext = new BotDbContext(_dbOptions);
        var firstRoundLosers = new[] { "drafeukun", "palapapop", "dragonillis", "naiike", "lerucestlarue" };
        foreach (var userId in firstRoundLosers)
        {
            var record = await dbContext.TournamentRecords.FindAsync([userId, "arcade"]);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(record.WinsCount, Is.EqualTo(0), $"WinsCount for {userId}");
                Assert.That(record.RunnerUpCount, Is.EqualTo(0), $"RunnerUpCount for {userId}");
                Assert.That(record.ThirdPlaceCount, Is.EqualTo(0), $"ThirdPlaceCount for {userId}");
            }
        }
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIncrementExistingRecord_WhenPlayerAlreadyHasStats()
    {
        await using (var seedContext = new BotDbContext(_dbOptions))
        {
            await seedContext.TournamentRecords.AddAsync(new TournamentRecord
            {
                UserId = "pujolly",
                RoomId = "arcade",
                TournamentsEnteredCount = 5,
                WinsCount = 2,
                RunnerUpCount = 1,
                ThirdPlaceCount = 0,
                WonGames = 8,
                PlayedGames = 9
            });
            await seedContext.SaveChangesAsync();
        }

        var parts = new[] { "", "tournament", "end", TOUR_JSON };

        await _handler.HandleReceivedMessageAsync(parts, "arcade");

        await using var dbContext = new BotDbContext(_dbOptions);
        var record = await dbContext.TournamentRecords.FindAsync(["pujolly", "arcade"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.TournamentsEnteredCount, Is.EqualTo(6));
            Assert.That(record.WinsCount, Is.EqualTo(3));
            Assert.That(record.WonGames, Is.EqualTo(11));
            Assert.That(record.PlayedGames, Is.EqualTo(12));
        }
    }

    [Test]
    public void Test_HandleReceivedMessageAsync_ShouldNotThrow_WhenDbFactoryThrowsException()
    {
        _botDbContextFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("DB connection failed"));

        var parts = new[] { "", "tournament", "end", TOUR_JSON };

        Assert.DoesNotThrowAsync(() => _handler.HandleReceivedMessageAsync(parts, "arcade"));
    }
}
