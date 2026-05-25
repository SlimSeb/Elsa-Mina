using System.Globalization;
using ElsaMina.Commands.Tournaments.Betting;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;

namespace ElsaMina.UnitTests.Commands.Tournaments.Betting;

[TestFixture]
public class TournamentBettingServiceTest
{
    private IBot _bot;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IResourcesService _resourcesService;
    private IRoomsManager _roomsManager;
    private IClockService _clockService;
    private IBotDbContextFactory _botDbContextFactory;
    private IRoomUserDataService _roomUserDataService;
    private DbContextOptions<BotDbContext> _dbOptions;
    private TournamentBettingService _service;

    [SetUp]
    public void SetUp()
    {
        _bot = Substitute.For<IBot>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _resourcesService = Substitute.For<IResourcesService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _clockService = Substitute.For<IClockService>();
        _botDbContextFactory = Substitute.For<IBotDbContextFactory>();
        _roomUserDataService = Substitute.For<IRoomUserDataService>();

        _configuration.Name.Returns("Elsa-Mina");
        _configuration.Trigger.Returns("-");
        _configuration.DefaultLocaleCode.Returns("en-US");
        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<html/>");
        _resourcesService.GetString("bet_resolution_correct_guesses", Arg.Any<CultureInfo>())
            .Returns("🏆 {0} won! Correct guesses: {1}");
        _resourcesService.GetString("bet_resolution_nobody_correct", Arg.Any<CultureInfo>())
            .Returns("🏆 {0} won! Nobody guessed correctly.");

        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _botDbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _service = new TournamentBettingService(_bot, _templatesManager, _configuration,
            _resourcesService, _roomsManager, _clockService, _botDbContextFactory, _roomUserDataService);
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.Database.EnsureDeletedAsync();
    }

    // ── AnnounceBetsAsync ──────────────────────────────────────────────────

    [Test]
    public async Task Test_AnnounceBetsAsync_ShouldSendHtmlToRoom()
    {
        await _service.AnnounceBetsAsync(["PlayerA", "PlayerB"], "room1");

        _bot.Received(1).Say("room1", Arg.Is<string>(s => s.StartsWith("/adduhtml betting-room1,")));
    }

    [Test]
    public async Task Test_AnnounceBetsAsync_ShouldOpenBettingSession_SoPlaceBetSucceeds()
    {
        await _service.AnnounceBetsAsync(["playerA", "playerB"], "room1");

        var result = await _service.PlaceBetAsync("bettor1", "playera", "room1");

        Assert.That(result, Is.EqualTo(BetPlacementError.Success));
    }

    [Test]
    public async Task Test_AnnounceBetsAsync_ShouldNormalizePlayers_ToLowerAlphaNum()
    {
        await _service.AnnounceBetsAsync(["Player A!"], "room1");

        var result = await _service.PlaceBetAsync("bettor1", "playera", "room1");

        Assert.That(result, Is.EqualTo(BetPlacementError.Success));
    }

    // ── PlaceBetAsync ──────────────────────────────────────────────────────

    [Test]
    public async Task Test_PlaceBetAsync_ShouldReturnNoBettingSession_WhenNoActiveSession()
    {
        var result = await _service.PlaceBetAsync("bettor1", "playerA", "room1");

        Assert.That(result, Is.EqualTo(BetPlacementError.NoBettingSession));
    }

    [Test]
    public async Task Test_PlaceBetAsync_ShouldReturnInvalidPlayer_WhenPlayerNotInTournament()
    {
        await _service.AnnounceBetsAsync(["playerA"], "room1");

        var result = await _service.PlaceBetAsync("bettor1", "nobody", "room1");

        Assert.That(result, Is.EqualTo(BetPlacementError.InvalidPlayer));
    }

    [Test]
    public async Task Test_PlaceBetAsync_ShouldReturnSuccess_WhenBetIsValid()
    {
        await _service.AnnounceBetsAsync(["playerA", "playerB"], "room1");

        var result = await _service.PlaceBetAsync("bettor1", "playera", "room1");

        Assert.That(result, Is.EqualTo(BetPlacementError.Success));
    }

    [Test]
    public async Task Test_PlaceBetAsync_ShouldReturnAlreadyBet_WhenBettorAlreadyHasABet()
    {
        await _service.AnnounceBetsAsync(["playerA", "playerB"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");

        var result = await _service.PlaceBetAsync("bettor1", "playerb", "room1");

        Assert.That(result, Is.EqualTo(BetPlacementError.AlreadyBet));
    }

    [Test]
    public async Task Test_PlaceBetAsync_ShouldReturnSuccess_WhenDifferentBettorsTargetSamePlayer()
    {
        await _service.AnnounceBetsAsync(["playerA"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");

        var result = await _service.PlaceBetAsync("bettor2", "playera", "room1");

        Assert.That(result, Is.EqualTo(BetPlacementError.Success));
    }

    [Test]
    public async Task Test_PlaceBetAsync_ShouldReturnBettingClosed_WhenWindowHasExpired()
    {
        var now = DateTimeOffset.UtcNow;
        _clockService.CurrentUtcDateTimeOffset.Returns(now);
        await _service.AnnounceBetsAsync(["playerA"], "room1");

        _clockService.CurrentUtcDateTimeOffset.Returns(now.AddSeconds(61));

        var result = await _service.PlaceBetAsync("bettor1", "playera", "room1");

        Assert.That(result, Is.EqualTo(BetPlacementError.BettingClosed));
    }

    [Test]
    public async Task Test_PlaceBetAsync_ShouldReturnSuccess_WhenWindowHasNotExpired()
    {
        var now = DateTimeOffset.UtcNow;
        _clockService.CurrentUtcDateTimeOffset.Returns(now);
        await _service.AnnounceBetsAsync(["playerA"], "room1");

        _clockService.CurrentUtcDateTimeOffset.Returns(now.AddSeconds(59));

        var result = await _service.PlaceBetAsync("bettor1", "playera", "room1");

        Assert.That(result, Is.EqualTo(BetPlacementError.Success));
    }

    [Test]
    public async Task Test_PlaceBetAsync_ShouldBeIsolatedByRoom()
    {
        await _service.AnnounceBetsAsync(["playerA"], "room1");

        var result = await _service.PlaceBetAsync("bettor1", "playera", "room2");

        Assert.That(result, Is.EqualTo(BetPlacementError.NoBettingSession));
    }

    // ── CancelBetAsync ─────────────────────────────────────────────────────

    [Test]
    public async Task Test_CancelBetAsync_ShouldReturnZero_WhenNoActiveSession()
    {
        var count = await _service.CancelBetAsync("bettor1", "room1");

        Assert.That(count, Is.Zero);
    }

    [Test]
    public async Task Test_CancelBetAsync_ShouldReturnZero_WhenNoBetExists()
    {
        await _service.AnnounceBetsAsync(["playerA"], "room1");

        var count = await _service.CancelBetAsync("bettor1", "room1");

        Assert.That(count, Is.Zero);
    }

    [Test]
    public async Task Test_CancelBetAsync_ShouldReturnOne_AndAllowNewBet_AfterCancellation()
    {
        await _service.AnnounceBetsAsync(["playerA", "playerB"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");

        var count = await _service.CancelBetAsync("bettor1", "room1");

        Assert.That(count, Is.EqualTo(1));
        var result = await _service.PlaceBetAsync("bettor1", "playerb", "room1");
        Assert.That(result, Is.EqualTo(BetPlacementError.Success));
    }

    [Test]
    public async Task Test_CancelBetAsync_ShouldReturnZero_WhenSpecifiedPlayerDoesNotMatchBet()
    {
        await _service.AnnounceBetsAsync(["playerA", "playerB"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");

        var count = await _service.CancelBetAsync("bettor1", "room1", "playerb");

        Assert.That(count, Is.Zero);
    }

    [Test]
    public async Task Test_CancelBetAsync_ShouldNotAffectOtherBettors()
    {
        await _service.AnnounceBetsAsync(["playerA"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");
        await _service.PlaceBetAsync("bettor2", "playera", "room1");

        await _service.CancelBetAsync("bettor1", "room1");

        var result = await _service.PlaceBetAsync("bettor2", "playera", "room1");
        Assert.That(result, Is.EqualTo(BetPlacementError.AlreadyBet));
    }

    // ── ResolveBetsAsync ───────────────────────────────────────────────────

    [Test]
    public async Task Test_ResolveBetsAsync_ShouldDoNothing_WhenNoBetsExist()
    {
        await _service.ResolveBetsAsync("playerA", "room1");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_ResolveBetsAsync_ShouldAnnounceCorrectGuessers_WhenSomeGuessedRight()
    {
        await _service.AnnounceBetsAsync(["playerA", "playerB"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");
        await _service.PlaceBetAsync("bettor2", "playerb", "room1");

        await _service.ResolveBetsAsync("playera", "room1");

        _bot.Received(1).Say("room1", Arg.Is<string>(s => s.Contains("bettor1") && !s.Contains("bettor2")));
    }

    [Test]
    public async Task Test_ResolveBetsAsync_ShouldAnnounceNobodyCorrect_WhenNoCorrectBets()
    {
        await _service.AnnounceBetsAsync(["playerA", "playerB"], "room1");
        await _service.PlaceBetAsync("bettor1", "playerb", "room1");

        await _service.ResolveBetsAsync("playera", "room1");

        _bot.Received(1).Say("room1", Arg.Is<string>(s => s.Contains("Nobody")));
    }

    [Test]
    public async Task Test_ResolveBetsAsync_ShouldUseRoomCulture_WhenLookingUpStrings()
    {
        var culture = new CultureInfo("fr-FR");
        var room = Substitute.For<IRoom>();
        room.Culture.Returns(culture);
        _roomsManager.GetRoom("room1").Returns(room);

        await _service.AnnounceBetsAsync(["playerA"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");

        await _service.ResolveBetsAsync("playera", "room1");

        _resourcesService.Received().GetString("bet_resolution_correct_guesses", culture);
    }

    [Test]
    public async Task Test_ResolveBetsAsync_ShouldCleanUp_SoFurtherBetsAreRejected()
    {
        await _service.AnnounceBetsAsync(["playerA"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");

        await _service.ResolveBetsAsync("playera", "room1");

        var result = await _service.PlaceBetAsync("bettor2", "playera", "room1");
        Assert.That(result, Is.EqualTo(BetPlacementError.NoBettingSession));
    }

    [Test]
    public async Task Test_ResolveBetsAsync_ShouldIncrementCorrectBetsCount_ForBettorWhoGuessedRight()
    {
        await using var seedContext = new BotDbContext(_dbOptions);
        seedContext.RoomUsers.Add(new RoomUser { Id = "bettor1", RoomId = "room1" });
        await seedContext.SaveChangesAsync();

        await _service.AnnounceBetsAsync(["playerA", "playerB"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");

        await _service.ResolveBetsAsync("playera", "room1");

        await using var dbContext = new BotDbContext(_dbOptions);
        var record = await dbContext.BetRecords.FindAsync(["bettor1", "room1"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record, Is.Not.Null);
            Assert.That(record.CorrectBetsCount, Is.EqualTo(1));
            Assert.That(record.TotalBetsCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_ResolveBetsAsync_ShouldNotIncrementCorrectBetsCount_ForBettorWhoGuessedWrong()
    {
        await using var seedContext = new BotDbContext(_dbOptions);
        seedContext.RoomUsers.Add(new RoomUser { Id = "bettor1", RoomId = "room1" });
        await seedContext.SaveChangesAsync();

        await _service.AnnounceBetsAsync(["playerA", "playerB"], "room1");
        await _service.PlaceBetAsync("bettor1", "playerb", "room1");

        await _service.ResolveBetsAsync("playera", "room1");

        await using var dbContext = new BotDbContext(_dbOptions);
        var record = await dbContext.BetRecords.FindAsync(["bettor1", "room1"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record, Is.Not.Null);
            Assert.That(record.CorrectBetsCount, Is.Zero);
            Assert.That(record.TotalBetsCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_ResolveBetsAsync_ShouldSaveBetRecordsForAllBettors()
    {
        await using var seedContext = new BotDbContext(_dbOptions);
        seedContext.RoomUsers.Add(new RoomUser { Id = "bettor1", RoomId = "room1" });
        seedContext.RoomUsers.Add(new RoomUser { Id = "bettor2", RoomId = "room1" });
        await seedContext.SaveChangesAsync();

        await _service.AnnounceBetsAsync(["playerA", "playerB"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");
        await _service.PlaceBetAsync("bettor2", "playerb", "room1");

        await _service.ResolveBetsAsync("playera", "room1");

        await using var dbContext = new BotDbContext(_dbOptions);
        var records = dbContext.BetRecords.Where(r => r.RoomId == "room1").ToList();
        Assert.That(records.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Test_ResolveBetsAsync_ShouldAccumulateCounts_AcrossMultipleTournaments()
    {
        await using var seedContext = new BotDbContext(_dbOptions);
        seedContext.RoomUsers.Add(new RoomUser { Id = "bettor1", RoomId = "room1" });
        await seedContext.SaveChangesAsync();

        await _service.AnnounceBetsAsync(["playerA"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");
        await _service.ResolveBetsAsync("playera", "room1");

        await _service.AnnounceBetsAsync(["playerA"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");
        await _service.ResolveBetsAsync("playera", "room1");

        await using var dbContext = new BotDbContext(_dbOptions);
        var record = await dbContext.BetRecords.FindAsync(["bettor1", "room1"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.CorrectBetsCount, Is.EqualTo(2));
            Assert.That(record.TotalBetsCount, Is.EqualTo(2));
        }
    }

    // ── ReturnBetsAsync ────────────────────────────────────────────────────

    [Test]
    public async Task Test_ReturnBetsAsync_ShouldCleanUp_SoFurtherBetsAreRejected()
    {
        await _service.AnnounceBetsAsync(["playerA"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");

        await _service.ReturnBetsAsync("room1");

        var result = await _service.PlaceBetAsync("bettor2", "playera", "room1");
        Assert.That(result, Is.EqualTo(BetPlacementError.NoBettingSession));
    }

    [Test]
    public async Task Test_ReturnBetsAsync_ShouldNotSayAnything()
    {
        await _service.AnnounceBetsAsync(["playerA"], "room1");
        _bot.ClearReceivedCalls();

        await _service.ReturnBetsAsync("room1");

        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_ReturnBetsAsync_ShouldNotSaveBetRecords()
    {
        await _service.AnnounceBetsAsync(["playerA"], "room1");
        await _service.PlaceBetAsync("bettor1", "playera", "room1");

        await _service.ReturnBetsAsync("room1");

        await _botDbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }
}
