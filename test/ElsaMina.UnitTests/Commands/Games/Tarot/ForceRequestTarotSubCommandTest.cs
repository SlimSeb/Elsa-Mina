using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

public class ForceRequestTarotSubCommandTest
{
    private IRoomsManager _roomsManager;
    private IContext _context;
    private ForceRequestTarotSubCommand _command;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.GetRoom(Arg.Any<string>()).ReturnsNull();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("contextroom");
        _command = new ForceRequestTarotSubCommand(_roomsManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriverInRoomsAndPrivateMessages()
    {
        // Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
        Assert.That(_command.RequiredRankInPrivateMessage, Is.EqualTo(Rank.Driver));
    }

    [Test]
    [TestCase(true, "French Room, alice", ExpectedResult = "frenchroom")]
    [TestCase(true, ", alice", ExpectedResult = null)]
    [TestCase(false, "alice", ExpectedResult = "contextroom")]
    public string Test_GetTargetRoomId_ShouldReturnRoomBeforePlayer_WhenInPrivateMessage(bool isPrivateMessage,
        string target)
    {
        // Arrange
        _context.IsPrivateMessage.Returns(isPrivateMessage);
        _context.Target.Returns(target);

        // Act & Assert
        return _command.GetTargetRoomId(_context);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotRunning_WhenRoomIsMissingInPrivateMessage()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns(", alice");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("tarot_not_running");
    }
}
