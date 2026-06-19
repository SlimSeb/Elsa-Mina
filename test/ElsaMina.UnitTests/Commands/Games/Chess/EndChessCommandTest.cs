using ElsaMina.Commands.Games.Chess;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Chess;

public class EndChessCommandTest
{
    private EndChessCommand _command;
    private IContext _context;
    private IRoom _room;
    private IChessGame _chessGame;

    [SetUp]
    public void SetUp()
    {
        _command = new EndChessCommand();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _chessGame = Substitute.For<IChessGame>();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelGame_WhenGameIsChess()
    {
        _context.Room.Returns(_room);
        _room.Game.Returns(_chessGame);

        await _command.RunAsync(_context);

        _chessGame.Received(1).Cancel();
        _context.Received(1).ReplyLocalizedMessage("chess_game_cancelled");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyOngoingGameMessage_WhenNoChessGameExists()
    {
        _context.Room.Returns(_room);
        _room.Game.Returns(Substitute.For<IGame>());

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("chess_game_ongoing_game");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyOngoingGameMessage_WhenRoomIsNull()
    {
        _context.Room.ReturnsNull();

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("chess_game_ongoing_game");
    }
}
