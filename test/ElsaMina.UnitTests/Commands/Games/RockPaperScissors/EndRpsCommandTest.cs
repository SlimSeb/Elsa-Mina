using ElsaMina.Commands.Games.RockPaperScissors;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.RockPaperScissors;

[TestFixture]
public class EndRpsCommandTest
{
    private EndRpsCommand _command;
    private IContext _mockContext;
    private IRoom _mockRoom;
    private IRpsGame _mockRpsGame;

    [SetUp]
    public void SetUp()
    {
        _command = new EndRpsCommand();
        _mockContext = Substitute.For<IContext>();
        _mockRoom = Substitute.For<IRoom>();
        _mockRpsGame = Substitute.For<IRpsGame>();

        _mockContext.Room.Returns(_mockRoom);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelGameAndReply_WhenRpsGameIsActive()
    {
        _mockRoom.Game.Returns(_mockRpsGame);

        await _command.RunAsync(_mockContext);

        _mockRpsGame.Received(1).Cancel();
        _mockContext.Received(1).ReplyLocalizedMessage("rps_game_cancelled");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotRunning_WhenNoRpsGameExists()
    {
        _mockRoom.Game.ReturnsNull();

        await _command.RunAsync(_mockContext);

        _mockContext.Received(1).ReplyLocalizedMessage("rps_not_running");
        _mockRpsGame.DidNotReceive().Cancel();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotRunning_WhenDifferentGameIsActive()
    {
        _mockRoom.Game.Returns(Substitute.For<IGame>());

        await _command.RunAsync(_mockContext);

        _mockContext.Received(1).ReplyLocalizedMessage("rps_not_running");
        _mockRpsGame.DidNotReceive().Cancel();
    }
}
