using ElsaMina.Commands.Games.Chess;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Chess;

public class JoinChessCommandTest
{
    private IRoomsManager _roomsManager;
    private IContext _context;
    private JoinChessCommand _command;
    private IChessGame _chessGame;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _context = Substitute.For<IContext>();
        _chessGame = Substitute.For<IChessGame>();

        _command = new JoinChessCommand(_roomsManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldJoinGame_WhenRoomExistsAndHasChessGame()
    {
        var room = Substitute.For<IRoom>();
        room.Game.Returns(_chessGame);
        _roomsManager.GetRoom("room1").Returns(room);
        _context.Target.Returns("room1");
        var sender = Substitute.For<IUser>();
        _context.Sender.Returns(sender);

        await _command.RunAsync(_context);

        await _chessGame.Received(1).JoinGame(sender);
    }

    [Test]
    [TestCase(false, 1)]
    [TestCase(true, 0)]
    public async Task Test_RunAsync_ShouldDisplayAnnounce_WhenGameIsNotStarted(bool isStarted, int expectedAnnounces)
    {
        var room = Substitute.For<IRoom>();
        room.Game.Returns(_chessGame);
        _roomsManager.GetRoom("room1").Returns(room);
        _context.Target.Returns("room1");
        _context.Sender.Returns(Substitute.For<IUser>());
        _chessGame.IsStarted.Returns(isStarted);

        await _command.RunAsync(_context);

        await _chessGame.Received(expectedAnnounces).DisplayAnnounce();
    }
}
