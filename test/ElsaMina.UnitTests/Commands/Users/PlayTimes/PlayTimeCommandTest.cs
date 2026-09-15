using ElsaMina.Commands.Users.PlayTimes;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Users.PlayTimes;

public class PlayTimeCommandTest
{
    private IContext _context;
    private IUser _sender;
    private IBotDbContextFactory _dbContextFactory;
    private PlayTimeCommand _command;

    private static DbContextOptions<BotDbContext> CreateNewInMemoryOptions() =>
        new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [SetUp]
    public void SetUp()
    {
        _sender = Substitute.For<IUser>();
        _context = Substitute.For<IContext>();
        _context.Sender.Returns(_sender);
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _command = new PlayTimeCommand(_dbContextFactory);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoData_WhenUserNotFound()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Target.Returns("unknownuser");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("play_time_no_data", "unknownuser");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenDatabaseThrows()
    {
        // Arrange
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("db error"));
        _context.RoomId.Returns("lobby");
        _context.Target.Returns("alice");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("play_time_error");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithPlayTime_WhenUserFound()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        await dbContext.RoomUsers.AddAsync(new RoomUser
        {
            Id = "alice",
            RoomId = "lobby",
            PlayTime = TimeSpan.FromHours(5),
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Target.Returns("alice");
        var format = "d'd 'hh'h 'mm'm 'ss's'";
        _context.GetString("play_time_format").Returns(format);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("play_time_result", "Alice", TimeSpan.FromHours(5).ToString(format), "lobby");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseSenderId_WhenTargetIsEmpty()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        await dbContext.RoomUsers.AddAsync(new RoomUser
        {
            Id = "bob",
            RoomId = "lobby",
            PlayTime = TimeSpan.FromMinutes(90),
            User = new SavedUser { UserId = "bob", UserName = "Bob" }
        });
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Target.Returns(string.Empty);
        var format = "d'd 'hh'h 'mm'm 'ss's'";
        _context.GetString("play_time_format").Returns(format);
        _sender.UserId.Returns("bob");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("play_time_result", "Bob", TimeSpan.FromMinutes(90).ToString(format), "lobby");
    }

    [Test]
    public async Task Test_RunAsync_ShouldFallbackToUserId_WhenUserNameIsNull()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        await dbContext.RoomUsers.AddAsync(new RoomUser
        {
            Id = "orphan",
            RoomId = "lobby",
            PlayTime = TimeSpan.FromHours(3),
            User = new SavedUser { UserId = "orphan", UserName = null }
        });
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Target.Returns("orphan");
        var format = "d'd 'hh'h 'mm'm 'ss's'";
        _context.GetString("play_time_format").Returns(format);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("play_time_result", "orphan", TimeSpan.FromHours(3).ToString(format), "lobby");
    }

    [Test]
    public async Task Test_RunAsync_ShouldExcludeUsersFromOtherRooms()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        await dbContext.RoomUsers.AddAsync(new RoomUser
        {
            Id = "alice",
            RoomId = "otherroom",
            PlayTime = TimeSpan.FromHours(10),
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Target.Returns("alice");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("play_time_no_data", "alice");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseProvidedRoomId_WhenRoomParameterIsGiven()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        await dbContext.RoomUsers.AddAsync(new RoomUser
        {
            Id = "alice",
            RoomId = "otherroom",
            PlayTime = TimeSpan.FromHours(7),
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Target.Returns("alice, otherroom");
        var format = "d'd 'hh'h 'mm'm 'ss's'";
        _context.GetString("play_time_format").Returns(format);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("play_time_result", "Alice", TimeSpan.FromHours(7).ToString(format), "otherroom");
    }
}
