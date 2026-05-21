using ElsaMina.Commands.Games.RockPaperScissors;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.UnitTests.Fixtures;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.RockPaperScissors;

[TestFixture]
public class JoinRpsCommandTest
{
    private JoinRpsCommand _command;
    private IContext _mockContext;
    private IRoom _mockRoom;
    private IRpsGame _mockRpsGame;
    private IUser _mockSender;

    [SetUp]
    public void SetUp()
    {
        _command = new JoinRpsCommand();
        _mockContext = Substitute.For<IContext>();
        _mockRoom = Substitute.For<IRoom>();
        _mockRpsGame = Substitute.For<IRpsGame>();
        _mockSender = UserFixtures.VoicedUser("Player1");

        _mockContext.Room.Returns(_mockRoom);
        _mockContext.Sender.Returns(_mockSender);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotRunning_WhenNoRpsGameExists()
    {
        _mockRoom.Game.ReturnsNull();

        await _command.RunAsync(_mockContext);

        _mockContext.Received(1).ReplyLocalizedMessage("rps_not_running");
        await _mockRpsGame.DidNotReceive().Join(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotRunning_WhenDifferentGameIsActive()
    {
        _mockRoom.Game.Returns(Substitute.For<ElsaMina.Core.Services.Games.IGame>());

        await _command.RunAsync(_mockContext);

        _mockContext.Received(1).ReplyLocalizedMessage("rps_not_running");
    }

    [Test]
    public async Task Test_RunAsync_ShouldJoinGame_WhenRpsGameIsActive()
    {
        _mockRoom.Game.Returns(_mockRpsGame);
        _mockRpsGame.Join("Player1").Returns((true, "rps_join_success", new object[] { "Player1" }));

        await _command.RunAsync(_mockContext);

        await _mockRpsGame.Received(1).Join("Player1");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyErrorMessage_WhenJoinFails()
    {
        _mockRoom.Game.Returns(_mockRpsGame);
        _mockRpsGame.Join("Player1").Returns((false, "rps_game_full", Array.Empty<object>()));

        await _command.RunAsync(_mockContext);

        _mockContext.Received(1).ReplyLocalizedMessage("rps_game_full");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotReply_WhenJoinSucceeds()
    {
        _mockRoom.Game.Returns(_mockRpsGame);
        _mockRpsGame.Join("Player1").Returns((true, "rps_join_success", new object[] { "Player1" }));

        await _command.RunAsync(_mockContext);

        _mockContext.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>());
    }
}
