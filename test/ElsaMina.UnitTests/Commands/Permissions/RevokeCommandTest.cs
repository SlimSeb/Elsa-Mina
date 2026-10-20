using ElsaMina.Commands.Permissions;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Permissions;

public class RevokeCommandTest
{
    private const string ROOM_ID = "franais";
    private static readonly DateTimeOffset NOW = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private IRoomsManager _roomsManager;
    private IPermissionGrantStore _permissionGrantStore;
    private IPermissionTargetResolver _permissionTargetResolver;
    private IContext _context;
    private RevokeCommand _command;

    [SetUp]
    public void SetUp()
    {
        var room = Substitute.For<IRoom>();
        room.RoomId.Returns(ROOM_ID);
        room.Name.Returns("Français");
        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.GetRoom(Arg.Any<string>()).ReturnsNull();
        _roomsManager.GetRoom(ROOM_ID).Returns(room);
        _permissionGrantStore = Substitute.For<IPermissionGrantStore>();
        _permissionGrantStore.GetActiveGrants(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Array.Empty<PermissionGrant>());
        _permissionTargetResolver = Substitute.For<IPermissionTargetResolver>();

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns(ROOM_ID);

        _command = new RevokeCommand(_roomsManager, _permissionGrantStore, _permissionTargetResolver);
    }

    [Test]
    public void Test_Properties_ShouldBeRoomOwnerOnlyInRoomsAndPrivateMessages()
    {
        // Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.RoomOwner));
        Assert.That(_command.RequiredRankInPrivateMessage, Is.EqualTo(Rank.RoomOwner));
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    [TestCase(false, "alice, %", ExpectedResult = ROOM_ID)]
    [TestCase(true, "alice, %, otherroom", ExpectedResult = "otherroom")]
    public string Test_GetTargetRoomId_ShouldReturnLastArgument_WhenInPrivateMessage(bool isPrivateMessage,
        string target)
    {
        // Arrange
        _context.IsPrivateMessage.Returns(isPrivateMessage);
        _context.Target.Returns(target);

        // Act & Assert
        return _command.GetTargetRoomId(_context);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenRightIsMissing()
    {
        // Arrange
        _context.Target.Returns("alice");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("permissions_revoke_help");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenUserDoesNotHaveTheRight()
    {
        // Arrange
        _context.Target.Returns("Alice, add-badge");
        _permissionTargetResolver.Resolve("add-badge")
            .Returns(new PermissionTarget(PermissionGrantKind.Command, "add-badge"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("permissions_revoke_not_found", "Alice", "add-badge", "Français");
        await _permissionGrantStore.DidNotReceiveWithAnyArgs().RemoveAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldRemoveRankGrant_WhenAnyRankIsGiven()
    {
        // Arrange
        _context.Target.Returns("alice, @");
        _permissionTargetResolver.Resolve("@").Returns(new PermissionTarget(PermissionGrantKind.Rank, "@"));
        GivenGrants(CreateGrant(PermissionGrantKind.Rank, "+"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _permissionGrantStore.Received(1)
            .RemoveAsync(ROOM_ID, "alice", PermissionGrantKind.Rank, "+", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("permissions_revoke_success", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRemoveGrantBySavedTarget_WhenRightCannotBeResolvedAnymore()
    {
        // Arrange
        _context.Target.Returns("alice, deletedcommand");
        _permissionTargetResolver.Resolve("deletedcommand").ReturnsNull();
        GivenGrants(CreateGrant(PermissionGrantKind.Command, "deletedcommand"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _permissionGrantStore.Received(1).RemoveAsync(ROOM_ID, "alice", PermissionGrantKind.Command,
            "deletedcommand", Arg.Any<CancellationToken>());
    }

    private void GivenGrants(params PermissionGrant[] grants)
    {
        _permissionGrantStore.GetActiveGrants(ROOM_ID, "alice").Returns(grants);
    }

    private static PermissionGrant CreateGrant(PermissionGrantKind kind, string target) =>
        new(ROOM_ID, "alice", kind, target, "owner", NOW, null);
}
