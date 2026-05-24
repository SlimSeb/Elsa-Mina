using ElsaMina.Commands.Tournaments.Trade;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Tournaments.Trade;

public class TourPointsCommandTest
{
    private IContext _context;
    private IBotDbContextFactory _dbContextFactory;
    private DbContextOptions<BotDbContext> _dbOptions;
    private TourPointsCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");

        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _context.GetString(Arg.Any<string>()).Returns(string.Empty);

        _command = new TourPointsCommand(_dbContextFactory);
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
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenRecordDoesNotExist()
    {
        // Arrange
        _context.Target.Returns("Alice");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tour_points_not_found", "Alice", "arcade");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyResult_WhenRecordExists()
    {
        // Arrange
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "alice",
            RoomId = "arcade",
            WinsCount = 7
        });
        await dbContext.SaveChangesAsync();

        _context.Target.Returns("Alice");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tour_points_result", "Alice", 7, "arcade");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseExplicitRoomId_WhenProvidedInTarget()
    {
        // Arrange
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "alice",
            RoomId = "otherroom",
            WinsCount = 3
        });
        await dbContext.SaveChangesAsync();

        _context.Target.Returns("Alice, OtherRoom");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tour_points_result", "Alice", 3, "otherroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseContextRoomId_WhenRoomNotProvidedInTarget()
    {
        // Arrange
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "bob",
            RoomId = "arcade",
            WinsCount = 2
        });
        await dbContext.SaveChangesAsync();

        _context.RoomId.Returns("arcade");
        _context.Target.Returns("Bob");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tour_points_result", "Bob", 2, "arcade");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenRecordExistsInDifferentRoom()
    {
        // Arrange
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.TournamentRecords.AddAsync(new TournamentRecord
        {
            UserId = "alice",
            RoomId = "otherroom",
            WinsCount = 5
        });
        await dbContext.SaveChangesAsync();

        _context.Target.Returns("Alice");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tour_points_not_found", "Alice", "arcade");
    }
}
