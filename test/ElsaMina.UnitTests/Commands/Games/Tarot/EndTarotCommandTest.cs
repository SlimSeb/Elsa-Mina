using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class EndTarotCommandTest
{
    private EndTarotCommand _command;
    private IContext _context;
    private IRoom _room;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _context.Room.Returns(_room);

        _command = new EndTarotCommand();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotRunning_WhenNoTarotGame()
    {
        _room.Game.ReturnsNull();

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tarot_not_running");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelGame_WhenTarotGameIsActive()
    {
        var game = Substitute.For<ITarotGame>();
        _room.Game.Returns(game);

        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            game.Received(1).Cancel();
            _context.Received(1).ReplyLocalizedMessage("tarot_game_cancelled");
        }
    }
}
