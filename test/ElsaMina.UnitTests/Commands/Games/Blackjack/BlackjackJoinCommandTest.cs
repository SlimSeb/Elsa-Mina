using ElsaMina.Commands.Games.Blackjack;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Blackjack;

public class BlackjackJoinCommandTest
{
    private IRoomsManager _roomsManager;
    private BlackjackJoinCommand _command;
    private IContext _context;
    private IRoom _room;
    private IUser _sender;
    private IBlackjackGame _game;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _command = new BlackjackJoinCommand(_roomsManager);

        _context = Substitute.For<IContext>();
        _context.Target.Returns("casino");
        _room = Substitute.For<IRoom>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("player");
        _context.Sender.Returns(_sender);
        _game = Substitute.For<IBlackjackGame>();
        _roomsManager.GetRoom("casino").Returns(_room);
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        Assert.That(_command.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldStartPrivateGameForSender_WhenRoomHasPendingGame()
    {
        // Arrange
        _room.Game.Returns(_game);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).StartGame();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Owner, Is.SameAs(_sender));
            Assert.That(_game.Context, Is.SameAs(_context));
            Assert.That(_game.IsPrivateMode, Is.True);
            Assert.That(_game.TargetRoomId, Is.EqualTo("casino"));
            Assert.That(_game.TargetUserId, Is.EqualTo("player"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenGameAlreadyStarted()
    {
        // Arrange
        _game.IsStarted.Returns(true);
        _room.Game.Returns(_game);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.DidNotReceive().StartGame();
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenRoomGameIsNotBlackjack()
    {
        // Arrange
        var otherGame = Substitute.For<IGame>();
        _room.Game.Returns(otherGame);

        // Act
        await _command.RunAsync(_context);

        // Assert
        Assert.That(otherGame.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenRoomIsUnknown()
    {
        // Arrange
        _roomsManager.GetRoom("casino").ReturnsNull();

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.DidNotReceive().StartGame();
    }
}
