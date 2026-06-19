using ElsaMina.Commands.Games.Chess;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Chess;

public class ForfeitChessCommandTest
{
    private ForfeitChessCommand _command;
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

        _command = new ForfeitChessCommand(_roomsManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldForfeit_WhenGameIsChess()
    {
        _context.Target.Returns("room1");
        _roomsManager.GetRoom("room1").Returns(_room);
        _room.Game.Returns(_chessGame);

        await _command.RunAsync(_context);

        await _chessGame.Received(1).Forfeit(_context.Sender);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("room1");
        _roomsManager.GetRoom("room1").ReturnsNull();

        await _command.RunAsync(_context);

        await _chessGame.DidNotReceive().Forfeit(Arg.Any<IUser>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenGameIsNotChess()
    {
        _context.Target.Returns("room1");
        _roomsManager.GetRoom("room1").Returns(_room);
        _room.Game.Returns(Substitute.For<IGame>());

        await _command.RunAsync(_context);

        await _chessGame.DidNotReceive().Forfeit(Arg.Any<IUser>());
    }
}
