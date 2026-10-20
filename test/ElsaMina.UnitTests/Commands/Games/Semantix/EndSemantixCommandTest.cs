using ElsaMina.Commands.Games.Semantix;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Semantix;

public class EndSemantixCommandTest
{
    private IRoomsManager _roomsManager;
    private ISemantixGameManager _gameManager;
    private EndSemantixCommand _command;
    private IContext _context;
    private IUser _sender;
    private ISemantixGame _game;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<ISemantixGameManager>();
        _command = new EndSemantixCommand(_roomsManager, _gameManager);

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("player");
        _context.Sender.Returns(_sender);
        _game = Substitute.For<ISemantixGame>();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenPrivateMessageHasNoRoom()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _gameManager.DidNotReceive().GetGame(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenNoGameIsRunning()
    {
        // Arrange
        _gameManager.GetGame("arcade", "player").ReturnsNull();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("sx_game_no_game");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelRoomGame_WhenGameIsRunning()
    {
        // Arrange
        _gameManager.GetGame("arcade", "player").Returns(_game);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).CancelAsync();
        _context.Received(1).ReplyLocalizedMessage("sx_game_cancelled");
        _game.DidNotReceive().Context = Arg.Any<IContext>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelPrivateGame_WhenEndedFromPrivateMessage()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns(" casino ");
        _roomsManager.GetRoom("casino").Returns(Substitute.For<IRoom>());
        _game.IsPrivateMode.Returns(true);
        _gameManager.GetGame("casino", "player").Returns(_game);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).CancelAsync();
        Assert.That(_game.Context, Is.SameAs(_context));
    }
}
