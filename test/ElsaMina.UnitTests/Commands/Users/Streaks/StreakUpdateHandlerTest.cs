using ElsaMina.Commands.Users.Streaks;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Users.Streaks;

public class StreakUpdateHandlerTest
{
    private StreakUpdateHandler _handler;
    private IStreakService _streakService;
    private IClockService _clockService;
    private IConfiguration _configuration;
    private IContext _context;
    private IRoom _room;
    private IUser _sender;

    [SetUp]
    public void SetUp()
    {
        var contextFactory = Substitute.For<IContextFactory>();
        _streakService = Substitute.For<IStreakService>();
        _clockService = Substitute.For<IClockService>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _sender = Substitute.For<IUser>();

        _configuration.Name.Returns("botname");
        _room.TimeZone.Returns(TimeZoneInfo.Utc);
        _context.Room.Returns(_room);
        _context.Sender.Returns(_sender);
        _context.RoomId.Returns("testroom");
        _sender.UserId.Returns("alice");
        _clockService.CurrentUtcDateTime.Returns(new DateTime(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc));

        _handler = new StreakUpdateHandler(contextFactory, _streakService, _clockService, _configuration);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotUpdateStreak_WhenSenderIsBot()
    {
        // Arrange
        _sender.UserId.Returns("botname");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _streakService.DidNotReceiveWithAnyArgs()
            .UpdateStreakAsync(default, default, default);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldCallUpdateStreak_ForRegularUser()
    {
        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _streakService.Received(1)
            .UpdateStreakAsync("alice", "testroom", new DateOnly(2026, 5, 13));
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldConvertToRoomTimezone_WhenComputingDate()
    {
        // Arrange —yo UTC midnight, room is UTC+2, so room date is May 14
        _clockService.CurrentUtcDateTime.Returns(new DateTime(2026, 5, 13, 23, 30, 0, DateTimeKind.Utc));
        var utcPlus2 = TimeZoneInfo.CreateCustomTimeZone("utc+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
        _room.TimeZone.Returns(utcPlus2);

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _streakService.Received(1)
            .UpdateStreakAsync("alice", "testroom", new DateOnly(2026, 5, 14));
    }
}
