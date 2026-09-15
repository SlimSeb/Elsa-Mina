using ElsaMina.Commands.Badges;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Badges;

public class GiveBadgeCommandTest
{
    private IContext _context;
    private IRoomUserDataService _roomUserDataService;
    private IBotDbContextFactory _dbContextFactory;
    private GiveBadgeCommand _command;

    private static DbContextOptions<BotDbContext> CreateNewInMemoryOptions()
    {
        return new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _command = new GiveBadgeCommand(_roomUserDataService, _dbContextFactory);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        // Act & Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldReturnCorrectKey()
    {
        // Act & Assert
        Assert.That(_command.HelpMessageKey, Is.EqualTo("badge_give_help_message"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithCouldNotFindBadgeMessage_WhenBadgeNotFound()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.Target.Returns("UserId,NonExistingBadge");
        _context.RoomId.Returns("room1");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("badge_give_could_not_find_badge", "nonexistingbadge");
        await _roomUserDataService.DidNotReceive().GiveBadgeToUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldGiveBadgeToUserAndReplyWithSuccessMessage_WhenValidArguments()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        var badge = new Badge { Id = "existingbadge", Name = "Existing Badge", RoomId = "room1" };
        await dbContext.Badges.AddAsync(badge);
        await dbContext.SaveChangesAsync();
        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.Target.Returns("UserId,ExistingBadge");
        _context.RoomId.Returns("room1");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _roomUserDataService.Received(1).GiveBadgeToUserAsync("room1", "userid", "existingbadge");
        _context.Received(1).ReplyLocalizedMessage("badge_give_success", "userid", "Existing Badge");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeUserIdAndBadgeId_ToLowerAlphaNum()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        var badge = new Badge { Id = "coolbadge456", Name = "Cool Badge", RoomId = "room1" };
        await dbContext.Badges.AddAsync(badge);
        await dbContext.SaveChangesAsync();
        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.Target.Returns("Test-User_123!,Cool-Badge_456!");
        _context.RoomId.Returns("room1");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _roomUserDataService.Received(1).GiveBadgeToUserAsync("room1", "testuser123", "coolbadge456");
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimWhitespace_FromUserIdAndBadgeId()
    {
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        var badge = new Badge { Id = "badgeid", Name = "Badge", RoomId = "room1" };
        await dbContext.Badges.AddAsync(badge);
        await dbContext.SaveChangesAsync();
        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.Target.Returns("  UserId  ,  BadgeId  ");
        _context.RoomId.Returns("room1");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).GiveBadgeToUserAsync("room1", "userid", "badgeid");
    }

    [Test]
    public async Task Test_RunAsync_ShouldFindBadge_UsingBadgeIdAndRoomId()
    {
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        await dbContext.Badges.AddAsync(new Badge { Id = "badge", Name = "Badge", RoomId = "specificroom" });
        await dbContext.SaveChangesAsync();
        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.Target.Returns("user,badge");
        _context.RoomId.Returns("specificroom");

        await _command.RunAsync(_context);

        var freshDbContext = new BotDbContext(options);
        var found = await freshDbContext.Badges.FindAsync("badge", "specificroom");
        Assert.That(found, Is.Not.Null);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseCorrectRoomId_FromContext()
    {
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        var badge = new Badge { Id = "badge", Name = "Badge", RoomId = "myroom" };
        await dbContext.Badges.AddAsync(badge);
        await dbContext.SaveChangesAsync();
        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.Target.Returns("user,badge");
        _context.RoomId.Returns("myroom");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).GiveBadgeToUserAsync("myroom", Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassBadgeName_InSuccessMessage()
    {
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        var badge = new Badge { Id = "badge", Name = "Awesome Badge", RoomId = "room1" };
        await dbContext.Badges.AddAsync(badge);
        await dbContext.SaveChangesAsync();
        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.Target.Returns("user,badge");
        _context.RoomId.Returns("room1");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("badge_give_success", "user", "Awesome Badge");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotCallGiveBadge_WhenBadgeNotFound()
    {
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.Target.Returns("user,nonexistent");
        _context.RoomId.Returns("room1");

        await _command.RunAsync(_context);

        await _roomUserDataService.DidNotReceive().GiveBadgeToUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}