using ElsaMina.Commands.Games.Chess;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Chess;

public class PlayChessCommandTest
{
    private PlayChessCommand _command;
    private IRoomsManager _roomsManager;
    private IContext _context;
    private IRoom _room;
    private IChessGame _chessGame;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _chessGame = Substitute.For<IChessGame>();

        _command = new PlayChessCommand(_roomsManager);
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        Assert.That(_command.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldMakeMove_WhenGameIsChess()
    {
        _context.Target.Returns("room1, e2e4");
        _roomsManager.GetRoom("room1").Returns(_room);
        _room.Game.Returns(_chessGame);

        await _command.RunAsync(_context);

        await _chessGame.Received(1).Play(_context.Sender, "e2e4");
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("room1, e2e4");
        _roomsManager.GetRoom("room1").ReturnsNull();

        await _command.RunAsync(_context);

        await _chessGame.DidNotReceive().Play(Arg.Any<IUser>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenGameIsNotChess()
    {
        _context.Target.Returns("room1, e2e4");
        _roomsManager.GetRoom("room1").Returns(_room);
        _room.Game.Returns(Substitute.For<IGame>());

        await _command.RunAsync(_context);

        await _chessGame.DidNotReceive().Play(Arg.Any<IUser>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenTargetHasNoMove()
    {
        _context.Target.Returns("room1");
        _roomsManager.GetRoom("room1").Returns(_room);
        _room.Game.Returns(_chessGame);

        await _command.RunAsync(_context);

        await _chessGame.DidNotReceive().Play(Arg.Any<IUser>(), Arg.Any<string>());
    }
}
