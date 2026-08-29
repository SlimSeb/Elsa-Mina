using ElsaMina.Commands.Users.Streaks;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Users.Streaks;

public class StreakCommandTest
{
    private StreakCommand _command;
    private IStreakService _streakService;
    private IContext _context;
    private IUser _sender;
    private IRoom _room;

    [SetUp]
    public void SetUp()
    {
        _streakService = Substitute.For<IStreakService>();
        _context = Substitute.For<IContext>();
        _sender = Substitute.For<IUser>();
        _room = Substitute.For<IRoom>();

        _sender.UserId.Returns("alice");
        _context.Sender.Returns(_sender);
        _context.RoomId.Returns("testroom");
        _context.Room.Returns(_room);
        _room.GetParameterValueAsync(Parameter.StreaksEnabled, Arg.Any<CancellationToken>())
            .Returns(true.ToString());

        _command = new StreakCommand(_streakService);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseSenderUserId_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);
        _streakService.GetStreakAsync("alice", "testroom").Returns((5, 10));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _streakService.Received(1).GetStreakAsync("alice", "testroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseNormalizedTarget_WhenTargetIsProvided()
    {
        // Arrange
        _context.Target.Returns(" Bob ");
        _streakService.GetStreakAsync("bob", "testroom").Returns((3, 7));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _streakService.Received(1).GetStreakAsync("bob", "testroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoData_WhenStreakIsZero()
    {
        // Arrange
        _context.Target.Returns(string.Empty);
        _streakService.GetStreakAsync("alice", "testroom").Returns((0, 0));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("streak_no_data", "alice");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyResult_WhenStreakDataExists()
    {
        // Arrange
        _context.Target.Returns(string.Empty);
        _streakService.GetStreakAsync("alice", "testroom").Returns((5, 10));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("streak_result", "alice", 5, 10);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenExceptionOccurs()
    {
        // Arrange
        _context.Target.Returns(string.Empty);
        _streakService.GetStreakAsync(Arg.Any<string>(), Arg.Any<string>())
            .Throws(new Exception("DB error"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("streak_error");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyDisabled_WhenStreaksAreDisabled()
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.StreaksEnabled, Arg.Any<CancellationToken>())
            .Returns(false.ToString());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("streak_disabled");
        await _streakService.DidNotReceiveWithAnyArgs().GetStreakAsync(default, default, default);
    }
}
