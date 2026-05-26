using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Rooms;

public class RoomsManagerTest
{
    private RoomsManager _sut;
    private IRoomFactory _roomFactory;

    [SetUp]
    public void Setup()
    {
        _roomFactory = Substitute.For<IRoomFactory>();
        _sut = new RoomsManager(_roomFactory);
    }

    [Test]
    public async Task Test_InitializeRoomAsync_ShouldDelegateToFactoryAndStoreRoom()
    {
        // Arrange
        const string roomId = "testroom";
        var lines = new[] { "|title|Test Room", "|users|,user1" };
        var room = Substitute.For<IRoom>();
        room.RoomId.Returns(roomId);
        _roomFactory.CreateRoomAsync(roomId, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(room);

        // Act
        await _sut.InitializeRoomAsync(roomId, lines);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sut.HasRoom(roomId), Is.True);
            Assert.That(_sut.GetRoom(roomId), Is.SameAs(room));
        }
        await _roomFactory.Received(1).CreateRoomAsync(roomId, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RemoveRoom_ShouldRemoveRoomFromRegistry()
    {
        // Arrange
        const string roomId = "testroom";
        var room = Substitute.For<IRoom>();
        room.RoomId.Returns(roomId);
        _roomFactory.CreateRoomAsync(roomId, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(room);
        await _sut.InitializeRoomAsync(roomId, ["|title|Test"]);

        // Act
        _sut.RemoveRoom(roomId);

        // Assert
        Assert.That(_sut.HasRoom(roomId), Is.False);
    }

    [Test]
    public async Task Test_AddUserToRoom_ShouldCallAddUserOnRoom()
    {
        // Arrange
        const string roomId = "testroom";
        var room = Substitute.For<IRoom>();
        room.RoomId.Returns(roomId);
        _roomFactory.CreateRoomAsync(roomId, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(room);
        await _sut.InitializeRoomAsync(roomId, ["|title|Test"]);

        // Act
        _sut.AddUserToRoom(roomId, "+NewUser");

        // Assert
        room.Received(1).AddUser("+NewUser");
    }

    [Test]
    public async Task Test_RemoveUserFromRoom_ShouldCallRemoveUserOnRoom()
    {
        // Arrange
        const string roomId = "testroom";
        var room = Substitute.For<IRoom>();
        room.RoomId.Returns(roomId);
        _roomFactory.CreateRoomAsync(roomId, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(room);
        await _sut.InitializeRoomAsync(roomId, ["|title|Test"]);

        // Act
        _sut.RemoveUserFromRoom(roomId, "@SomeUser");

        // Assert
        room.Received(1).RemoveUser("@SomeUser");
    }

    [Test]
    public async Task Test_RenameUserInRoom_ShouldCallRenameUserOnRoom()
    {
        // Arrange
        const string roomId = "testroom";
        var room = Substitute.For<IRoom>();
        room.RoomId.Returns(roomId);
        _roomFactory.CreateRoomAsync(roomId, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(room);
        await _sut.InitializeRoomAsync(roomId, ["|title|Test"]);

        // Act
        _sut.RenameUserInRoom(roomId, "OldName", "NewName");

        // Assert
        room.Received(1).RenameUser("OldName", "NewName");
    }

    [Test]
    public async Task Test_Clear_ShouldRemoveAllRooms()
    {
        // Arrange
        var room1 = Substitute.For<IRoom>();
        room1.RoomId.Returns("room1");
        var room2 = Substitute.For<IRoom>();
        room2.RoomId.Returns("room2");
        _roomFactory.CreateRoomAsync("room1", Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(room1);
        _roomFactory.CreateRoomAsync("room2", Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(room2);
        await _sut.InitializeRoomAsync("room1", ["|title|Room1"]);
        await _sut.InitializeRoomAsync("room2", ["|title|Room2"]);

        // Act
        _sut.Clear();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sut.HasRoom("room1"), Is.False);
            Assert.That(_sut.HasRoom("room2"), Is.False);
        }
    }
}
