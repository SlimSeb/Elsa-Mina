using ElsaMina.Commands.Tournaments.Trade;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Tournaments.Trade;

public class TradePointsCommandTest
{
    private IContext _context;
    private IBotDbContextFactory _dbContextFactory;
    private DbContextOptions<BotDbContext> _dbOptions;
    private TradePointsCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>()).Returns(string.Empty);

        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _context.GetString(Arg.Any<string>()).Returns(string.Empty);

        _command = new TradePointsCommand(_dbContextFactory);
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.Database.EnsureDeletedAsync();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        // Arrange
        // (no additional setup)

        // Act
        var rank = _command.RequiredRank;

        // Assert
        Assert.That(rank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        // Arrange
        // (no additional setup)

        // Act
        var allowed = _command.IsAllowedInPrivateMessage;

        // Assert
        Assert.That(allowed, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>());
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetHasFewerThanThreeParts()
    {
        // Arrange
        _context.Target.Returns("alice, bob");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>());
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidPoints_WhenPointsIsNotANumber()
    {
        // Arrange
        _context.Target.Returns("alice, bob, notanumber");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tradepoints_invalid_points");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidPoints_WhenPointsIsZero()
    {
        // Arrange
        _context.Target.Returns("alice, bob, 0");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tradepoints_invalid_points");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidPoints_WhenPointsIsNegative()
    {
        // Arrange
        _context.Target.Returns("alice, bob, -5");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tradepoints_invalid_points");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotEnough_WhenOriginRecordDoesNotExist()
    {
        // Arrange
        _context.Target.Returns("alice, bob, 3");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tradepoints_not_enough", "alice");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotEnough_WhenOriginHasInsufficientPoints()
    {
        // Arrange
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "alice",
            RoomId = "arcade",
            WinsCount = 2
        });
        await dbContext.SaveChangesAsync();

        _context.Target.Returns("alice, bob, 5");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tradepoints_not_enough", "alice");
    }

    [Test]
    public async Task Test_RunAsync_ShouldTransferPoints_WhenBothRecordsExist()
    {
        // Arrange
        await using var seedContext = new BotDbContext(_dbOptions);
        await seedContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "alice",
            RoomId = "arcade",
            WinsCount = 10
        });
        await seedContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "bob",
            RoomId = "arcade",
            WinsCount = 2
        });
        await seedContext.SaveChangesAsync();

        _context.Target.Returns("alice, bob, 3");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using var assertContext = new BotDbContext(_dbOptions);
        var aliceRecord = await assertContext.TournamentRecords.FindAsync(["alice", "arcade"]);
        var bobRecord = await assertContext.TournamentRecords.FindAsync(["bob", "arcade"]);

        Assert.That(aliceRecord.WinsCount, Is.EqualTo(7));
        Assert.That(bobRecord.WinsCount, Is.EqualTo(5));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateRecipientRecord_WhenRecipientHasNoRecord()
    {
        // Arrange
        await using var seedContext = new BotDbContext(_dbOptions);
        await seedContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "alice",
            RoomId = "arcade",
            WinsCount = 10
        });
        await seedContext.SaveChangesAsync();

        _context.Target.Returns("alice, bob, 4");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using var assertContext = new BotDbContext(_dbOptions);
        var bobRecord = await assertContext.TournamentRecords.FindAsync(["bob", "arcade"]);

        Assert.That(bobRecord, Is.Not.Null);
        Assert.That(bobRecord.WinsCount, Is.EqualTo(4));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySuccess_WhenTradeSucceeds()
    {
        // Arrange
        await using var seedContext = new BotDbContext(_dbOptions);
        await seedContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "alice",
            RoomId = "arcade",
            WinsCount = 10
        });
        await seedContext.SaveChangesAsync();

        _context.Target.Returns("alice, bob, 4");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tradepoints_success", 4, "alice", "bob");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotifyStaffRoom_WhenTradeSucceeds()
    {
        // Arrange
        await using var seedContext = new BotDbContext(_dbOptions);
        await seedContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "alice",
            RoomId = "arcade",
            WinsCount = 10
        });
        await seedContext.SaveChangesAsync();

        _context.Target.Returns("alice, bob, 4");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).SendMessageIn(
            "frenchstaff",
            Arg.Is<string>(msg => msg.Contains("trade-alice-4")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseExplicitRoomId_WhenProvidedInTarget()
    {
        // Arrange
        await using var seedContext = new BotDbContext(_dbOptions);
        await seedContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "alice",
            RoomId = "otherroom",
            WinsCount = 10
        });
        await seedContext.SaveChangesAsync();

        _context.Target.Returns("alice, bob, 3, OtherRoom");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using var assertContext = new BotDbContext(_dbOptions);
        var aliceRecord = await assertContext.TournamentRecords.FindAsync(["alice", "otherroom"]);
        Assert.That(aliceRecord.WinsCount, Is.EqualTo(7));
    }
}
