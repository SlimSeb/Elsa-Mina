using ElsaMina.Commands.Games.VoltorbFlip;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.VoltorbFlip;

public class EndVoltorbFlipCommandTest
{
    // Concrete IUser to avoid NSubstitute auto-substitute sharing between Owner and Sender
    private record TestUser(string UserId) : IUser
    {
        public string Name => UserId;
        public bool IsIdle => false;
        public Rank Rank => Rank.Regular;
    }

    private IRoomsManager _roomsManager;
    private IVoltorbFlipGameManager _gameManager;
    private EndVoltorbFlipCommand _command;
    private IContext _context;
    private IRoom _room;
    private IVoltorbFlipGame _voltorbFlipGame;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<IVoltorbFlipGameManager>();
        _command = new EndVoltorbFlipCommand(_roomsManager, _gameManager);
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _voltorbFlipGame = Substitute.For<IVoltorbFlipGame>();
    }

    [Test]
    public void Test_RequiredRank_ShouldReturnVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelGame_WhenVoltorbFlipGameExists()
    {
        // Arrange
        _context.Room.Returns(_room);
        _room.Game.Returns(_voltorbFlipGame);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _voltorbFlipGame.Received(1).CancelAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyCancelled_WhenVoltorbFlipGameExists()
    {
        // Arrange
        _context.Room.Returns(_room);
        _room.Game.Returns(_voltorbFlipGame);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("vf_game_cancelled");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenRoomIsNull()
    {
        // Arrange
        _context.Room.ReturnsNull();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("vf_game_no_game");
        await _voltorbFlipGame.DidNotReceive().CancelAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenRoomHasNoGame()
    {
        // Arrange
        _context.Room.Returns(_room);
        _room.Game.ReturnsNull();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("vf_game_no_game");
        await _voltorbFlipGame.DidNotReceive().CancelAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotOwner_WhenSenderIsNotOwner()
    {
        // Arrange - use concrete TestUser to avoid NSubstitute auto-substitute sharing
        var owner = new TestUser("owner-id");
        var sender = new TestUser("sender-id");
        _voltorbFlipGame.Owner = owner;
        _context.Sender.Returns(sender);
        _context.Room.Returns(_room);
        _room.Game.Returns(_voltorbFlipGame);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("vf_game_not_owner");
        await _voltorbFlipGame.DidNotReceive().CancelAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelGame_WhenSenderIsNotOwnerButHasDriverRank()
    {
        // Arrange
        var owner = new TestUser("owner-id");
        var sender = new TestUser("sender-id");
        _voltorbFlipGame.Owner = owner;
        _context.Sender.Returns(sender);
        _context.HasRankOrHigher(Rank.Driver).Returns(true);
        _context.Room.Returns(_room);
        _room.Game.Returns(_voltorbFlipGame);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _voltorbFlipGame.Received(1).CancelAsync();
        _context.Received(1).ReplyLocalizedMessage("vf_game_cancelled");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenOtherGameExists()
    {
        // Arrange
        _context.Room.Returns(_room);
        _room.Game.Returns(Substitute.For<IGame>());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("vf_game_no_game");
        await _voltorbFlipGame.DidNotReceive().CancelAsync();
    }
}
