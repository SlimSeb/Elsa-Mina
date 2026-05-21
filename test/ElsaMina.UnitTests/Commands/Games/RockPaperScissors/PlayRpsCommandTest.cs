using ElsaMina.Commands.Games.RockPaperScissors;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.UnitTests.Fixtures;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.RockPaperScissors;

[TestFixture]
public class PlayRpsCommandTest
{
    private IRoomsManager _mockRoomsManager;
    private IContext _mockContext;
    private IRoom _mockRoom;
    private IRpsGame _mockRpsGame;
    private IUser _mockSender;

    [SetUp]
    public void SetUp()
    {
        _mockRoomsManager = Substitute.For<IRoomsManager>();
        _mockContext = Substitute.For<IContext>();
        _mockRoom = Substitute.For<IRoom>();
        _mockRpsGame = Substitute.For<IRpsGame>();
        _mockSender = UserFixtures.VoicedUser("Player1");

        _mockContext.Sender.Returns(_mockSender);
        _mockContext.Target.Returns("testroom");
        _mockRoom.Game.Returns(_mockRpsGame);
        _mockRoomsManager.GetRoom("testroom").Returns(_mockRoom);
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        var command = new RockRpsCommand(_mockRoomsManager);
        Assert.That(command.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        var command = new RockRpsCommand(_mockRoomsManager);
        Assert.That(command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RockCommand_ShouldPlayRock_WhenRpsGameIsActive()
    {
        var command = new RockRpsCommand(_mockRoomsManager);

        await command.RunAsync(_mockContext);

        await _mockRpsGame.Received(1).Play("player1", RpsChoice.Rock);
    }

    [Test]
    public async Task Test_PaperCommand_ShouldPlayPaper_WhenRpsGameIsActive()
    {
        var command = new PaperRpsCommand(_mockRoomsManager);

        await command.RunAsync(_mockContext);

        await _mockRpsGame.Received(1).Play("player1", RpsChoice.Paper);
    }

    [Test]
    public async Task Test_ScissorsCommand_ShouldPlayScissors_WhenRpsGameIsActive()
    {
        var command = new ScissorsRpsCommand(_mockRoomsManager);

        await command.RunAsync(_mockContext);

        await _mockRpsGame.Received(1).Play("player1", RpsChoice.Scissors);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenRoomDoesNotExist()
    {
        _mockRoomsManager.GetRoom("testroom").ReturnsNull();
        var command = new RockRpsCommand(_mockRoomsManager);

        await command.RunAsync(_mockContext);

        await _mockRpsGame.DidNotReceive().Play(Arg.Any<string>(), Arg.Any<RpsChoice>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenRoomHasNoRpsGame()
    {
        _mockRoom.Game.Returns(Substitute.For<IGame>());
        var command = new RockRpsCommand(_mockRoomsManager);

        await command.RunAsync(_mockContext);

        await _mockRpsGame.DidNotReceive().Play(Arg.Any<string>(), Arg.Any<RpsChoice>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenRoomHasNoGame()
    {
        _mockRoom.Game.ReturnsNull();
        var command = new RockRpsCommand(_mockRoomsManager);

        await command.RunAsync(_mockContext);

        await _mockRpsGame.DidNotReceive().Play(Arg.Any<string>(), Arg.Any<RpsChoice>());
    }
}
