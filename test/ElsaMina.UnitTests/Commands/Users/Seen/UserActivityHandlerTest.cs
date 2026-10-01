using ElsaMina.Commands.Users.Seen;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Users.Seen;

public class UserActivityHandlerTest
{
    private const string ROOM_ID = "room1";
    private const string USER_NAME = "user123";

    private IUserSaveQueue _userSaveQueue;
    private UserActivityHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _userSaveQueue = Substitute.For<IUserSaveQueue>();
        _handler = new UserActivityHandler(_userSaveQueue);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSaveChatting_WhenUserSendsChatMessage()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(["", "c:", "123", USER_NAME, "hello"], ROOM_ID);

        // Assert
        _userSaveQueue.Received(1).Enqueue(USER_NAME, ROOM_ID, UserAction.Chatting);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSaveJoining_WhenUserJoins()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(["", "J", USER_NAME], ROOM_ID);

        // Assert
        _userSaveQueue.Received(1).Enqueue(USER_NAME, ROOM_ID, UserAction.Joining);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSaveLeaving_WhenUserLeaves()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(["", "L", USER_NAME], ROOM_ID);

        // Assert
        _userSaveQueue.Received(1).Enqueue(USER_NAME, ROOM_ID, UserAction.Leaving);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSaveNothing_WhenMessageIsTruncated()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(["", "c:", "123"], ROOM_ID);
        await _handler.HandleReceivedMessageAsync(["", "J"], ROOM_ID);
        await _handler.HandleReceivedMessageAsync([""], ROOM_ID);

        // Assert
        _userSaveQueue.DidNotReceiveWithAnyArgs().Enqueue(default, default, default);
    }
}
