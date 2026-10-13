using System.Globalization;
using ElsaMina.Commands.Games.Blackjack;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Blackjack;

public class BlackjackHitCommandTest
{
    private IRoomsManager _roomsManager;
    private IBlackjackGameManager _gameManager;
    private BlackjackHitCommand _command;
    private IContext _context;
    private IRoom _room;
    private IUser _sender;
    private IBlackjackGame _game;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<IBlackjackGameManager>();
        _command = new BlackjackHitCommand(_roomsManager, _gameManager);

        _context = Substitute.For<IContext>();
        _context.Target.Returns("casino");
        _room = Substitute.For<IRoom>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("player");
        _context.Sender.Returns(_sender);
        _game = Substitute.For<IBlackjackGame>();
        _roomsManager.GetRoom("casino").Returns(_room);
        _gameManager.GetGame("casino", "player").ReturnsNull();
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        Assert.That(_command.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns("");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _gameManager.DidNotReceive().GetGame(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenNoGameIsFound()
    {
        // Arrange
        _room.Game.ReturnsNull();

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.DidNotReceive().Hit(Arg.Any<IUser>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldHitPrivateGame_AndUseRoomCulture()
    {
        // Arrange
        var culture = new CultureInfo("de-DE");
        _room.Culture.Returns(culture);
        _game.IsPrivateMode.Returns(true);
        _gameManager.GetGame("casino", "player").Returns(_game);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).Hit(_sender);
        _context.Received().Culture = culture;
        Assert.That(_game.Context, Is.SameAs(_context));
    }

    [Test]
    public async Task Test_RunAsync_ShouldHitRoomGame_WhenNoPrivateGameExists()
    {
        // Arrange
        _room.Game.Returns(_game);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).Hit(_sender);
        _game.DidNotReceive().Context = Arg.Any<IContext>();
    }
}
