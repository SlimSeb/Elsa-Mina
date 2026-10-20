using ElsaMina.Commands.Games.Blackjack;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Blackjack;

public class BlackjackEndCommandTest
{
    private IRoomsManager _roomsManager;
    private IBlackjackGameManager _gameManager;
    private BlackjackEndCommand _command;
    private IContext _context;
    private IRoom _room;
    private IUser _sender;
    private IBlackjackGame _game;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<IBlackjackGameManager>();
        _command = new BlackjackEndCommand(_roomsManager, _gameManager);

        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("player");
        _context.Sender.Returns(_sender);
        _context.Room.Returns(_room);
        _game = Substitute.For<IBlackjackGame>();
    }

    private static IUser CreateUser(string userId)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(userId);
        return user;
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
        _context.Target.Returns(" ");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenPrivateGameDoesNotExist()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("casino");
        _roomsManager.GetRoom("casino").ReturnsNull();
        _gameManager.GetGame("casino", "player").ReturnsNull();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("bj_game_no_game");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelPrivateGame_WhenItExists()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("casino");
        _roomsManager.GetRoom("casino").Returns(_room);
        _gameManager.GetGame("casino", "player").Returns(_game);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).CancelAsync();
        Assert.That(_game.Context, Is.SameAs(_context));
        _context.Received(1).ReplyLocalizedMessage("bj_game_cancelled");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenRoomHasNoBlackjackGame()
    {
        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("bj_game_no_game");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelRoomGame_WhenSenderIsOwner()
    {
        // Arrange
        _game.Owner.Returns(_sender);
        _room.Game.Returns(_game);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).CancelAsync();
        _context.Received(1).ReplyLocalizedMessage("bj_game_cancelled");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelRoomGame_WhenNobodyJoinedYet()
    {
        // Arrange
        _game.Owner.ReturnsNull();
        _room.Game.Returns(_game);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).CancelAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldRefuse_WhenSenderIsNotOwnerNorStaff()
    {
        // Arrange
        var owner = CreateUser("owner");
        _game.Owner.Returns(owner);
        _room.Game.Returns(_game);
        _context.HasRankOrHigher(Rank.Driver).Returns(false);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.DidNotReceive().CancelAsync();
        _context.Received(1).ReplyLocalizedMessage("bj_game_not_owner");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelRoomGame_WhenSenderIsStaff()
    {
        // Arrange
        var owner = CreateUser("owner");
        _game.Owner.Returns(owner);
        _room.Game.Returns(_game);
        _context.HasRankOrHigher(Rank.Driver).Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).CancelAsync();
    }
}
