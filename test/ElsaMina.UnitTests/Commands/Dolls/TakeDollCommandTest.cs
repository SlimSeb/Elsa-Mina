using ElsaMina.Commands.Dolls;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Dolls;

public class TakeDollCommandTest
{
    private IContext _context;
    private IRoomUserDataService _roomUserDataService;
    private TakeDollCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _command = new TakeDollCommand(_roomUserDataService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenArgumentsAreMissing()
    {
        // Arrange
        _context.Target.Returns("alice");
        _context.GetString("doll_take_help_message").Returns("Help message");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("doll_take_help_message");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().TakeDollFromUserAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNotOwned_WhenUserDoesNotOwnTheDoll()
    {
        // Arrange
        _context.Target.Returns("Alice, Pikachu");
        _context.RoomId.Returns("room1");
        _roomUserDataService
            .TakeDollFromUserAsync("room1", "alice", "pikachu", Arg.Any<CancellationToken>())
            .Throws(new ArgumentException());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("doll_take_not_owned", "alice", "pikachu");
    }

    [Test]
    public async Task Test_RunAsync_ShouldTakeDollAndReplyWithSuccess_WhenArgumentsAreValid()
    {
        // Arrange
        _context.Target.Returns("Alice, Pikachu");
        _context.RoomId.Returns("room1");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _roomUserDataService.Received(1)
            .TakeDollFromUserAsync("room1", "alice", "pikachu", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("doll_take_success", "alice", "pikachu");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAskForARoom_WhenInPrivateMessageWithoutRoom()
    {
        // Arrange
        _context.Target.Returns("Alice, Pikachu");
        _context.IsPrivateMessage.Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("doll_pm_missing_room");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().TakeDollFromUserAsync(default, default, default);
    }
}
