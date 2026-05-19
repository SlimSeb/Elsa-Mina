using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using System.Globalization;
using ElsaMina.Commands.Polls;

namespace ElsaMina.UnitTests.Commands.Polls;

public class ShowPollsCommandTest
{
    private ShowPollsCommand _sut;
    private IRoomsManager _roomsManager;
    private IBotDbContextFactory _dbContextFactory;
    private DbContextOptions<BotDbContext> _dbContextOptions;
    private IContext _context;
    private IRoom _room;

    private const string TestRoomId = "currentroom";
    private const string TargetRoomId = "targetroom";
    private readonly CultureInfo _culture = new CultureInfo("en-US");

    // Helper method to create unique in-memory options for each test run
    private DbContextOptions<BotDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    // Helper method to mock the factory to return a new context connected to the shared options
    private IBotDbContextFactory CreateFactory(DbContextOptions<BotDbContext> options)
    {
        var factory = Substitute.For<IBotDbContextFactory>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new BotDbContext(options)));
        return factory;
    }

    // Helper method to seed the database
    private async Task SeedDatabaseAsync(IEnumerable<SavedPoll> polls)
    {
        await using var context = new BotDbContext(_dbContextOptions);
        await context.Database.EnsureCreatedAsync();
        context.SavedPolls.AddRange(polls);
        await context.SaveChangesAsync();
    }

    [SetUp]
    public void Setup()
    {
        // Arrange
        _dbContextOptions = CreateOptions();
        _roomsManager = Substitute.For<IRoomsManager>();
        _dbContextFactory = CreateFactory(_dbContextOptions);
        _sut = new ShowPollsCommand(_roomsManager, _dbContextFactory);

        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _room.TimeZone.Returns(TimeZoneInfo.Utc);
        _context.RoomId.Returns(TestRoomId);
        _context.Room.Returns(_room);
        _context.Culture.Returns(_culture);
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(ci =>
                $"[{ci.Arg<string>()} {string.Join(", ", ci.Arg<object[]>().Select(o => o?.ToString() ?? "null"))}]");
    }

    [Test]
    public async Task RunAsync_WhenTargetIsCurrentRoomAndPollsExist_ShouldReplyWithHtmlPage()
    {
        // Arrange
        _context.Target.Returns("");
        var poll1 = new SavedPoll
        {
            Id = 101, RoomId = TestRoomId, Content = "Poll A",
            EndedAt = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc)
        };
        var poll2 = new SavedPoll
        {
            Id = 102, RoomId = TestRoomId, Content = "Poll B",
            EndedAt = new DateTime(2025, 1, 2, 11, 0, 0, DateTimeKind.Utc)
        };
        await SeedDatabaseAsync(new[] { poll1, poll2 });

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("show_polls_history_sent");
        _context.Received(1).ReplyHtmlPage(
            "polls-history",
            Arg.Is<string>(content =>
                content.Contains($"[show_polls_history_header {TestRoomId}]") &&
                content.Contains("Poll A") &&
                content.Contains("Poll B")));
    }

    [Test]
    public async Task RunAsync_WhenTargetIsSpecifiedRoomAndPollsExist_ShouldReplyWithHtmlPage()
    {
        // Arrange
        _context.Target.Returns(TargetRoomId.ToUpper());
        _roomsManager.HasRoom(TargetRoomId).Returns(true);
        var poll = new SavedPoll
        {
            Id = 201, RoomId = TargetRoomId, Content = "Target Poll",
            EndedAt = new DateTime(2025, 3, 3, 12, 0, 0, DateTimeKind.Utc)
        };
        await SeedDatabaseAsync(new[] { poll });

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("show_polls_history_sent");
        _context.Received(1).ReplyHtmlPage(
            "polls-history",
            Arg.Is<string>(content =>
                content.Contains($"[show_polls_history_header {TargetRoomId}]") &&
                content.Contains("Target Poll")));
    }

    [Test]
    public async Task RunAsync_WhenTargetIsCurrentRoomAndNoPollsExist_ShouldReplyNoPollsMessage()
    {
        // Arrange
        _context.Target.Returns("");

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("show_polls_no_polls", TestRoomId);
        _context.DidNotReceive().ReplyHtmlPage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task RunAsync_WhenTargetRoomDoesNotExist_ShouldReplyRoomNotExistMessage()
    {
        // Arrange
        _context.Target.Returns(TargetRoomId);
        _roomsManager.HasRoom(TargetRoomId).Returns(false);

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("show_polls_room_not_exist", TargetRoomId);
        _context.DidNotReceive().ReplyHtmlPage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task RunAsync_WhenTargetIsSpecifiedRoomButNoPollsExist_ShouldReplyNoPollsMessage()
    {
        // Arrange
        _context.Target.Returns(TargetRoomId);
        _roomsManager.HasRoom(TargetRoomId).Returns(true);
        // Seed polls for a DIFFERENT room
        await SeedDatabaseAsync(new[]
        {
            new SavedPoll { Id = 301, RoomId = "otherroom", Content = "Poll X", EndedAt = new DateTime(2025, 5, 5) }
        });

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("show_polls_no_polls", TargetRoomId);
        _context.DidNotReceive().ReplyHtmlPage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task RunAsync_WhenPollsExist_ShouldFormatPollDateUsingRoomTimeZone()
    {
        // Arrange
        _context.Target.Returns("");
        var customTimeZone = TimeZoneInfo.CreateCustomTimeZone("utc-plus-2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
        _room.TimeZone.Returns(customTimeZone);
        var poll = new SavedPoll
        {
            Id = 401,
            RoomId = TestRoomId,
            Content = "Timezone poll",
            EndedAt = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero)
        };
        await SeedDatabaseAsync(new[] { poll });
        var expectedDate = TimeZoneInfo.ConvertTime(poll.EndedAt, customTimeZone).ToString("G", _culture);

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtmlPage(
            "polls-history",
            Arg.Is<string>(content =>
                content.Contains(expectedDate) &&
                content.Contains("Timezone poll")));
    }
}
