using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.UserDetails;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Core.Services.Authorization;

public class AuthorizationServiceTest
{
    private const string CONTEXT_ROOM_ID = "franais";
    private const string OTHER_ROOM_ID = "otherroom";
    private const string USER_ID = "someuser";

    private IRoomsManager _roomsManager;
    private IUserDetailsManager _userDetailsManager;
    private IPermissionGrantStore _permissionGrantStore;
    private IContext _context;
    private ICommand _command;
    private AuthorizationService _authorizationService;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.GetRoom(Arg.Any<string>()).ReturnsNull();
        _userDetailsManager = Substitute.For<IUserDetailsManager>();
        _permissionGrantStore = Substitute.For<IPermissionGrantStore>();
        _permissionGrantStore.GetActiveGrants(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Array.Empty<PermissionGrant>());

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns(CONTEXT_ROOM_ID);
        _context.Sender.Returns(new User(USER_ID, Rank.Regular));

        _command = Substitute.For<ICommand>();
        _command.Name.Returns("add-badge");
        _command.Category.Returns("Badges");
        _command.RequiredRank.Returns(Rank.Driver);
        _command.RequiredRankInPrivateMessage.Returns(Rank.Driver);
        _command.GetTargetRoomId(Arg.Any<IContext>()).Returns(CONTEXT_ROOM_ID);

        _authorizationService = new AuthorizationService(_roomsManager, _userDetailsManager, _permissionGrantStore);
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldAllowWithoutLookingAtRooms_WhenRequiredRankIsRegular()
    {
        // Arrange
        _command.RequiredRank.Returns(Rank.Regular);

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.True);
        _command.DidNotReceive().GetTargetRoomId(Arg.Any<IContext>());
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldAllow_WhenSenderIsWhitelisted()
    {
        // Arrange
        _context.IsSenderWhitelisted.Returns(true);
        _command.RequiredRank.Returns(Rank.Admin);

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.True);
    }

    [Test]
    [TestCase(Rank.Regular, ExpectedResult = false)]
    [TestCase(Rank.Voiced, ExpectedResult = false)]
    [TestCase(Rank.Driver, ExpectedResult = true)]
    [TestCase(Rank.RoomOwner, ExpectedResult = true)]
    public async Task<bool> Test_AuthorizeAsync_ShouldUseSenderRank_WhenCommandActsInTheRoomOfTheMessage(Rank rank)
    {
        // Arrange
        _context.Sender.Returns(new User(USER_ID, rank));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.RoomId, Is.EqualTo(CONTEXT_ROOM_ID));
        Assert.That(result.RequiredRank, Is.EqualTo(Rank.Driver));
        _roomsManager.DidNotReceive().GetRoom(Arg.Any<string>());
        return result.IsAllowed;
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldCheckRankInTargetRoom_WhenCommandActsInAnotherRoom()
    {
        // Arrange
        _context.Sender.Returns(new User(USER_ID, Rank.RoomOwner));
        _command.GetTargetRoomId(_context).Returns(OTHER_ROOM_ID);
        GivenJoinedRoom(OTHER_ROOM_ID, new User(USER_ID, Rank.Voiced));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.False);
        Assert.That(result.RoomId, Is.EqualTo(OTHER_ROOM_ID));
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldUseRankInPrivateMessage_WhenCommandIsRunInPrivateMessage()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _command.RequiredRank.Returns(Rank.Driver);
        _command.RequiredRankInPrivateMessage.Returns(Rank.Regular);

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.True);
    }

    [Test]
    [TestCase(Rank.Voiced, ExpectedResult = false)]
    [TestCase(Rank.Driver, ExpectedResult = true)]
    public async Task<bool> Test_AuthorizeAsync_ShouldUseUserListOfTargetRoom_WhenPrivateMessageAndBotIsInTheRoom(
        Rank rankInRoom)
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Sender.Returns(new User(USER_ID, Rank.Admin));
        _command.GetTargetRoomId(_context).Returns(OTHER_ROOM_ID);
        GivenJoinedRoom(OTHER_ROOM_ID, new User(USER_ID, rankInRoom));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        await _userDetailsManager.DidNotReceive()
            .GetUserDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        return result.IsAllowed;
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldDeny_WhenPrivateMessageAndUserIsNotInTheTargetRoom()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _command.GetTargetRoomId(_context).Returns(OTHER_ROOM_ID);
        GivenJoinedRoom(OTHER_ROOM_ID);

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.False);
    }

    [Test]
    [TestCase("%otherroom", ExpectedResult = true)]
    [TestCase("+otherroom", ExpectedResult = false)]
    [TestCase("#anotherroom", ExpectedResult = false)]
    public async Task<bool> Test_AuthorizeAsync_ShouldAskServer_WhenPrivateMessageAndBotIsNotInTheTargetRoom(
        string roomKey)
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _command.GetTargetRoomId(_context).Returns(OTHER_ROOM_ID);
        _userDetailsManager.GetUserDetailsAsync(USER_ID, Arg.Any<CancellationToken>()).Returns(new UserDetailsDto
        {
            UserId = USER_ID,
            Rooms = new Dictionary<string, UserDetailsRoomDto> { [roomKey] = new() }
        });

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        return result.IsAllowed;
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldCheckContextRoom_WhenTargetRoomIsNull()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _command.GetTargetRoomId(_context).Returns((string)null);
        GivenJoinedRoom(CONTEXT_ROOM_ID, new User(USER_ID, Rank.Driver));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.True);
        Assert.That(result.RoomId, Is.EqualTo(CONTEXT_ROOM_ID));
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldDeny_WhenTargetRoomIsEmpty()
    {
        // Arrange
        _context.Sender.Returns(new User(USER_ID, Rank.RoomOwner));
        _command.GetTargetRoomId(_context).Returns(string.Empty);

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.False);
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldNormalizeTargetRoom_WhenBotIsNotInARoomWithThatExactId()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _command.GetTargetRoomId(_context).Returns(" Other Room ");
        GivenJoinedRoom(OTHER_ROOM_ID, new User(USER_ID, Rank.Driver));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.True);
        Assert.That(result.RoomId, Is.EqualTo(OTHER_ROOM_ID));
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldKeepExactRoomId_WhenBotIsInThatRoom()
    {
        // Arrange
        const string groupChatId = "groupchat-someone-test";
        _context.IsPrivateMessage.Returns(true);
        _command.GetTargetRoomId(_context).Returns(groupChatId);
        GivenJoinedRoom(groupChatId, new User(USER_ID, Rank.Driver));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.True);
        Assert.That(result.RoomId, Is.EqualTo(groupChatId));
    }

    [Test]
    [TestCase(Rank.Driver, ExpectedResult = true)]
    [TestCase(Rank.Mod, ExpectedResult = false)]
    public async Task<bool> Test_AuthorizeAsync_ShouldUseGrantedRank_WhenItIsHigherThanShowdownRank(
        Rank requiredRank)
    {
        // Arrange
        _command.RequiredRank.Returns(requiredRank);
        GivenGrants(CONTEXT_ROOM_ID, CreateGrant(PermissionGrantKind.Rank, "%"));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        return result.IsAllowed;
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldCapGrantedRank_WhenGrantIsAboveTheMaximum()
    {
        // Arrange
        _command.RequiredRank.Returns(Rank.RoomOwner);
        GivenGrants(CONTEXT_ROOM_ID, CreateGrant(PermissionGrantKind.Rank, "#"));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.False);
    }

    [Test]
    [TestCase(PermissionGrantKind.Command, "add-badge", ExpectedResult = true)]
    [TestCase(PermissionGrantKind.Command, "ADD-BADGE", ExpectedResult = true)]
    [TestCase(PermissionGrantKind.Command, "takebadge", ExpectedResult = false)]
    [TestCase(PermissionGrantKind.Category, "badges", ExpectedResult = true)]
    [TestCase(PermissionGrantKind.Category, "Dolls", ExpectedResult = false)]
    public async Task<bool> Test_AuthorizeAsync_ShouldAllow_WhenUserWasGrantedTheCommandOrItsCategory(
        PermissionGrantKind kind, string target)
    {
        // Arrange
        GivenGrants(CONTEXT_ROOM_ID, CreateGrant(kind, target));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        return result.IsAllowed;
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldIgnoreGrant_WhenItWasGivenInAnotherRoom()
    {
        // Arrange
        GivenGrants(OTHER_ROOM_ID, CreateGrant(PermissionGrantKind.Command, "add-badge", OTHER_ROOM_ID));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.False);
    }

    [Test]
    public async Task Test_AuthorizeAsync_ShouldIgnoreCommandGrant_WhenCommandIsWhitelistOnly()
    {
        // Arrange
        _command.IsWhitelistOnly.Returns(true);
        GivenGrants(CONTEXT_ROOM_ID, CreateGrant(PermissionGrantKind.Command, "add-badge"));

        // Act
        var result = await _authorizationService.AuthorizeAsync(_context, _command);

        // Assert
        Assert.That(result.IsAllowed, Is.False);
    }

    [Test]
    public async Task Test_GetEffectiveRankAsync_ShouldReturnAdmin_WhenSenderIsWhitelisted()
    {
        // Arrange
        _context.IsSenderWhitelisted.Returns(true);

        // Act
        var rank = await _authorizationService.GetEffectiveRankAsync(_context, OTHER_ROOM_ID);

        // Assert
        Assert.That(rank, Is.EqualTo(Rank.Admin));
    }

    [Test]
    public void Test_GetGrantedRank_ShouldReturnHighestActiveRankGrant()
    {
        // Arrange
        GivenGrants(CONTEXT_ROOM_ID,
            CreateGrant(PermissionGrantKind.Command, "add-badge"),
            CreateGrant(PermissionGrantKind.Rank, "%"));

        // Act
        var rank = _authorizationService.GetGrantedRank(CONTEXT_ROOM_ID, USER_ID);

        // Assert
        Assert.That(rank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_GetGrantedRank_ShouldReturnRegular_WhenUserHasNoGrant()
    {
        // Act
        var rank = _authorizationService.GetGrantedRank(CONTEXT_ROOM_ID, USER_ID);

        // Assert
        Assert.That(rank, Is.EqualTo(Rank.Regular));
    }

    private void GivenJoinedRoom(string roomId, params IUser[] users)
    {
        var room = Substitute.For<IRoom>();
        room.RoomId.Returns(roomId);
        room.Users.Returns(users.ToDictionary(user => user.UserId));
        _roomsManager.HasRoom(roomId).Returns(true);
        _roomsManager.GetRoom(roomId).Returns(room);
    }

    private void GivenGrants(string roomId, params PermissionGrant[] grants)
    {
        _permissionGrantStore.GetActiveGrants(roomId, USER_ID).Returns(grants);
    }

    private static PermissionGrant CreateGrant(PermissionGrantKind kind, string target,
        string roomId = CONTEXT_ROOM_ID) =>
        new(roomId, USER_ID, kind, target, "owner", DateTimeOffset.UnixEpoch, null);
}
