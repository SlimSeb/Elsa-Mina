using ElsaMina.Commands.Development;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.RoomInfo;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Development;

[TestFixture]
public class RoomInfoCommandTest
{
    private IRoomInfoManager _roomInfoManager;
    private IContext _context;
    private RoomInfoCommand _command;

    private const string TEST_ROOM_ID = "testroom";

    [SetUp]
    public void SetUp()
    {
        _roomInfoManager = Substitute.For<IRoomInfoManager>();
        _context = Substitute.For<IContext>();

        _context.RoomId.Returns(TEST_ROOM_ID);

        _command = new RoomInfoCommand(_roomInfoManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseCurrentRoom_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);
        _roomInfoManager.GetRoomInfoAsync(TEST_ROOM_ID, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RoomInfoDto { RoomId = TEST_ROOM_ID }));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _roomInfoManager.Received(1).GetRoomInfoAsync(TEST_ROOM_ID, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTarget_WhenTargetIsProvided()
    {
        // Arrange
        _context.Target.Returns("lobby");
        _roomInfoManager.GetRoomInfoAsync("lobby", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RoomInfoDto { RoomId = "lobby" }));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _roomInfoManager.Received(1).GetRoomInfoAsync("lobby", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoRoom_WhenTargetAndRoomIdAreEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);
        _context.RoomId.Returns((string)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("roominfo_no_room");
        await _roomInfoManager.DidNotReceive().GetRoomInfoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyTimeout_WhenRoomInfoIsNull()
    {
        // Arrange
        _context.Target.Returns(TEST_ROOM_ID);
        _roomInfoManager.GetRoomInfoAsync(TEST_ROOM_ID, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomInfoDto>(null));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("roominfo_timeout");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenRoomInfoHasError()
    {
        // Arrange
        _context.Target.Returns("unknownroom");
        _roomInfoManager.GetRoomInfoAsync("unknownroom", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RoomInfoDto { Id = "unknownroom", Error = "Room not found" }));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("roominfo_error", "Room not found");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithRoomInfoJson_WhenRoomInfoIsReceived()
    {
        // Arrange
        _context.Target.Returns(TEST_ROOM_ID);
        _roomInfoManager.GetRoomInfoAsync(TEST_ROOM_ID, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RoomInfoDto { RoomId = TEST_ROOM_ID, Title = "Test Room" }));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Is<string>(message =>
            message.StartsWith("!code ") && message.Contains("Test Room")));
    }
}
