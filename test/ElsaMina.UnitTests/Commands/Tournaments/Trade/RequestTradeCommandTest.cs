using ElsaMina.Commands.Tournaments.Trade;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Tournaments.Trade;

public class RequestTradeCommandTest
{
    private IContext _context;
    private IBotDbContextFactory _dbContextFactory;
    private IConfiguration _configuration;
    private DbContextOptions<BotDbContext> _dbOptions;
    private RequestTradeCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>()).Returns(string.Empty);

        var sender = Substitute.For<IUser>();
        sender.Name.Returns("Driver");
        _context.Sender.Returns(sender);

        _configuration = Substitute.For<IConfiguration>();
        _configuration.Trigger.Returns("-");

        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _context.GetString(Arg.Any<string>()).Returns(string.Empty);

        _command = new RequestTradeCommand(_dbContextFactory, _configuration);
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.Database.EnsureDeletedAsync();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        // Arrange
        // (no additional setup)

        // Act
        var rank = _command.RequiredRank;

        // Assert
        Assert.That(rank, Is.EqualTo(Rank.Voiced));
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
        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Any<string>());
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
        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidPoints_WhenPointsIsNotANumber()
    {
        // Arrange
        _context.Target.Returns("alice, bob, abc");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tradepoints_invalid_points");
        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidPoints_WhenPointsIsZeroOrNegative()
    {
        // Arrange
        _context.Target.Returns("alice, bob, 0");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tradepoints_invalid_points");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenOriginRecordDoesNotExist()
    {
        // Arrange
        _context.Target.Returns("alice, bob, 3");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tour_points_not_found", "alice", "arcade");
        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotEnough_WhenOriginHasInsufficientPoints()
    {
        // Arrange
        await using var seedContext = new BotDbContext(_dbOptions);
        await seedContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "alice",
            RoomId = "arcade",
            WinsCount = 2
        });
        await seedContext.SaveChangesAsync();

        _context.Target.Returns("alice, bob, 5");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tradepoints_not_enough", "alice");
        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRequestCreated_WhenTradeRequestIsValid()
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

        _context.Target.Returns("alice, bob, 3");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("requesttrade_created", 3, "alice", "bob", "arcade");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendApproveAndRefuseButtonsToStaffRoom_WhenTradeRequestIsValid()
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

        _context.Target.Returns("alice, bob, 3");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).SendMessageIn(
            "frenchstaff",
            Arg.Is<string>(msg =>
                msg.Contains("trade-req-alice-3") &&
                msg.Contains("-tradepoints alice, bob, 3, arcade") &&
                msg.Contains("-notrade alice, 3")));
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
        _context.Received(1).ReplyLocalizedMessage("requesttrade_created", 3, "alice", "bob", "otherroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTriggerFromConfiguration_WhenBuildingButtons()
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

        _configuration.Trigger.Returns("!");
        _context.Target.Returns("alice, bob, 3");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).SendMessageIn(
            "frenchstaff",
            Arg.Is<string>(msg =>
                msg.Contains("!tradepoints") &&
                msg.Contains("!notrade")));
    }
}
