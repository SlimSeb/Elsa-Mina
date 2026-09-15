using System.Globalization;
using ElsaMina.Commands.Users.PlayTimes;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Users.PlayTimes;

public class TopPlayTimesCommandTest
{
    private IContext _context;
    private ITemplatesManager _templatesManager;
    private IBotDbContextFactory _dbContextFactory;
    private IRoomsManager _roomsManager;
    private TopPlayTimesCommand _command;

    private static DbContextOptions<BotDbContext> CreateNewInMemoryOptions() =>
        new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.GetRoom(Arg.Any<string>()).Returns((IRoom)null);
        _command = new TopPlayTimesCommand(_dbContextFactory, _templatesManager, _roomsManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoData_WhenNoUsersHavePlayTime()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("top_play_times_no_data");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenDatabaseThrows()
    {
        // Arrange
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("db error"));
        _context.RoomId.Returns("lobby");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("top_play_times_error");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallTemplateWithCorrectViewModel_WhenUsersExist()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        await dbContext.RoomUsers.AddRangeAsync(
            new RoomUser
            {
                Id = "alice",
                RoomId = "lobby",
                PlayTime = TimeSpan.FromHours(10),
                User = new SavedUser { UserId = "alice", UserName = "Alice" }
            },
            new RoomUser
            {
                Id = "bob",
                RoomId = "lobby",
                PlayTime = TimeSpan.FromHours(5),
                User = new SavedUser { UserId = "bob", UserName = "Bob" }
            });
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Culture.Returns(CultureInfo.InvariantCulture);

        var expectedHtml = "<div>Top Play Times</div>";
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(expectedHtml);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Users/PlayTimes/TopPlayTimesTable",
            Arg.Is<TopPlayTimesViewModel>(vm =>
                vm.Room == "lobby" &&
                vm.Culture.Name == "" &&
                vm.TopList.Count() == 2));

        _context.Received(1).ReplyHtml(
            expectedHtml.RemoveNewlines().RemoveWhitespacesBetweenTags(),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderByPlayTimeDescending()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        await dbContext.RoomUsers.AddRangeAsync(
            new RoomUser
            {
                Id = "alice",
                RoomId = "lobby",
                PlayTime = TimeSpan.FromHours(2),
                User = new SavedUser { UserId = "alice", UserName = "Alice" }
            },
            new RoomUser
            {
                Id = "charlie",
                RoomId = "lobby",
                PlayTime = TimeSpan.FromHours(10),
                User = new SavedUser { UserId = "charlie", UserName = "Charlie" }
            },
            new RoomUser
            {
                Id = "bob",
                RoomId = "lobby",
                PlayTime = TimeSpan.FromHours(5),
                User = new SavedUser { UserId = "bob", UserName = "Bob" }
            });
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<div/>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Users/PlayTimes/TopPlayTimesTable",
            Arg.Is<TopPlayTimesViewModel>(vm =>
                vm.TopList.ElementAt(0).UserId == "charlie" && vm.TopList.ElementAt(0).Rank == 1 &&
                vm.TopList.ElementAt(1).UserId == "bob" && vm.TopList.ElementAt(1).Rank == 2 &&
                vm.TopList.ElementAt(2).UserId == "alice" && vm.TopList.ElementAt(2).Rank == 3));
    }

    [Test]
    public async Task Test_RunAsync_ShouldLimitResultsToTwenty()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        var roomUsers = Enumerable.Range(1, 25).Select(index => new RoomUser
        {
            Id = $"user{index}",
            RoomId = "lobby",
            PlayTime = TimeSpan.FromHours(index),
            User = new SavedUser { UserId = $"user{index}", UserName = $"User {index}" }
        });
        await dbContext.RoomUsers.AddRangeAsync(roomUsers);
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<div/>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Users/PlayTimes/TopPlayTimesTable",
            Arg.Is<TopPlayTimesViewModel>(vm => vm.TopList.Count() == 20));
    }

    [Test]
    public async Task Test_RunAsync_ShouldExcludeUsersWithZeroPlayTime()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        await dbContext.RoomUsers.AddRangeAsync(
            new RoomUser
            {
                Id = "alice",
                RoomId = "lobby",
                PlayTime = TimeSpan.FromHours(5),
                User = new SavedUser { UserId = "alice", UserName = "Alice" }
            },
            new RoomUser
            {
                Id = "bob",
                RoomId = "lobby",
                PlayTime = TimeSpan.Zero,
                User = new SavedUser { UserId = "bob", UserName = "Bob" }
            });
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<div/>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Users/PlayTimes/TopPlayTimesTable",
            Arg.Is<TopPlayTimesViewModel>(vm =>
                vm.TopList.Count() == 1 &&
                vm.TopList.First().UserId == "alice"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldExcludeUsersFromOtherRooms()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        await dbContext.RoomUsers.AddRangeAsync(
            new RoomUser
            {
                Id = "alice",
                RoomId = "lobby",
                PlayTime = TimeSpan.FromHours(10),
                User = new SavedUser { UserId = "alice", UserName = "Alice" }
            },
            new RoomUser
            {
                Id = "bob",
                RoomId = "otherroom",
                PlayTime = TimeSpan.FromHours(50),
                User = new SavedUser { UserId = "bob", UserName = "Bob" }
            });
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(dbContext);
        _context.RoomId.Returns("lobby");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<div/>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Users/PlayTimes/TopPlayTimesTable",
            Arg.Is<TopPlayTimesViewModel>(vm =>
                vm.TopList.Count() == 1 &&
                vm.TopList.First().UserId == "alice"));
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
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<div/>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Users/PlayTimes/TopPlayTimesTable",
            Arg.Is<TopPlayTimesViewModel>(vm =>
                vm.TopList.First().UserName == "orphan"));
    }
}