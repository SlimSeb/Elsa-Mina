using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class JoinTarotCommandTest
{
    private JoinTarotCommand _command;
    private IContext _context;
    private IRoom _room;
    private IUser _sender;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _sender = Substitute.For<IUser>();
        _sender.Name.Returns("player1");
        _context.Room.Returns(_room);
        _context.Sender.Returns(_sender);

        _command = new JoinTarotCommand();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotRunning_WhenNoTarotGame()
    {
        _room.Game.ReturnsNull();

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tarot_not_running");
    }

    [Test]
    public async Task Test_RunAsync_ShouldJoin_WhenTarotGameIsActive()
    {
        var game = Substitute.For<ITarotGame>();
        game.JoinAsync(_sender).Returns((true, "tarot_join_success", new object[] { "player1" }));
        _room.Game.Returns(game);

        await _command.RunAsync(_context);

        await game.Received(1).JoinAsync(_sender);
        _context.DidNotReceive().ReplyLocalizedMessage("tarot_join_success", Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRelayFailure_WhenJoinFails()
    {
        var game = Substitute.For<ITarotGame>();
        game.JoinAsync(_sender).Returns((false, "tarot_join_full", System.Array.Empty<object>()));
        _room.Game.Returns(game);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tarot_join_full", Arg.Any<object[]>());
    }
}
