using ElsaMina.Commands.Users.Streaks;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Users.Streaks;

public class StreakLeaderboardCommandTest
{
    private IContext _context;
    private ITemplatesManager _templatesManager;
    private IBotDbContextFactory _dbContextFactory;
    private IRoomsManager _roomsManager;
    private IRoom _room;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _room = Substitute.For<IRoom>();

        _context.RoomId.Returns("testroom");
        _context.Target.Returns(string.Empty);
        _room.Name.Returns("Test Room");
        _roomsManager.GetRoom("testroom").Returns(_room);

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("rendered-html");
    }

    private static RoomUser MakeUser(string id, string roomId, int current, int longest) =>
        new()
        {
            Id = id,
            RoomId = roomId,
            CurrentStreak = current,
            LongestStreak = longest,
            User = new SavedUser { UserId = id, UserName = id }
        };

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoData_WhenNoStreaksExistInRoom()
    {
        // Arrange
        await using var db = new BotDbContext(
            new DbContextOptionsBuilder<BotDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(db);

        // Act
        await new StreakLeaderboardCommand(_dbContextFactory, _templatesManager, _roomsManager)
            .RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("streak_leaderboard_no_data");
        await _templatesManager.DidNotReceiveWithAnyArgs().GetTemplateAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtml_WhenStreakDataExists()
    {
        // Arrange
        await using var db = new BotDbContext(
            new DbContextOptionsBuilder<BotDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.RoomUsers.AddRangeAsync(
            MakeUser("alice", "testroom", 5, 10),
            MakeUser("bob", "testroom", 3, 8));
        await db.SaveChangesAsync();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(db);

        // Act
        await new StreakLeaderboardCommand(_dbContextFactory, _templatesManager, _roomsManager)
            .RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml("rendered-html".RemoveNewlines().RemoveWhitespacesBetweenTags(),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderByCurrentStreakDescending()
    {
        // Arrange
        await using var db = new BotDbContext(
            new DbContextOptionsBuilder<BotDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.RoomUsers.AddRangeAsync(
            MakeUser("alice", "testroom", 2, 5),
            MakeUser("bob", "testroom", 7, 7),
            MakeUser("carol", "testroom", 4, 9));
        await db.SaveChangesAsync();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(db);

        // Act
        await new StreakLeaderboardCommand(_dbContextFactory, _templatesManager, _roomsManager)
            .RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Users/Streaks/StreakLeaderboard",
            Arg.Is<object>(vm => ((StreakLeaderboardViewModel)vm).TopList.ElementAt(0).UserId == "bob"
                && ((StreakLeaderboardViewModel)vm).TopList.ElementAt(1).UserId == "carol"
                && ((StreakLeaderboardViewModel)vm).TopList.ElementAt(2).UserId == "alice"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldExcludeUsersWithZeroCurrentStreak()
    {
        // Arrange
        await using var db = new BotDbContext(
            new DbContextOptionsBuilder<BotDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.RoomUsers.AddRangeAsync(
            MakeUser("alice", "testroom", 3, 5),
            MakeUser("bob", "testroom", 0, 10));
        await db.SaveChangesAsync();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(db);

        // Act
        await new StreakLeaderboardCommand(_dbContextFactory, _templatesManager, _roomsManager)
            .RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Users/Streaks/StreakLeaderboard",
            Arg.Is<object>(vm => ((StreakLeaderboardViewModel)vm).TopList.Count() == 1
                && ((StreakLeaderboardViewModel)vm).TopList.First().UserId == "alice"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTargetRoomId_WhenTargetIsProvided()
    {
        // Arrange
        await using var db = new BotDbContext(
            new DbContextOptionsBuilder<BotDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.RoomUsers.AddAsync(MakeUser("alice", "otherroom", 5, 5));
        await db.SaveChangesAsync();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(db);
        _context.Target.Returns("otherroom");
        _roomsManager.GetRoom("otherroom").Returns(_room);

        // Act
        await new StreakLeaderboardCommand(_dbContextFactory, _templatesManager, _roomsManager)
            .RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenExceptionOccurs()
    {
        // Arrange
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Throws(new Exception("DB error"));

        // Act
        await new StreakLeaderboardCommand(_dbContextFactory, _templatesManager, _roomsManager)
            .RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("streak_leaderboard_error");
    }
}
