using System.Globalization;
using ElsaMina.Commands.Permissions;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Permissions;

public class GrantCommandTest
{
    private const string ROOM_ID = "franais";
    private static readonly DateTimeOffset NOW = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private IRoomsManager _roomsManager;
    private IAuthorizationService _authorizationService;
    private IPermissionGrantStore _permissionGrantStore;
    private IPermissionTargetResolver _permissionTargetResolver;
    private ICommandRegistry _commandRegistry;
    private IClockService _clockService;
    private IContext _context;
    private IRoom _room;
    private GrantCommand _command;

    [SetUp]
    public void SetUp()
    {
        _room = Substitute.For<IRoom>();
        _room.RoomId.Returns(ROOM_ID);
        _room.Name.Returns("Français");
        _room.Culture.Returns(new CultureInfo("fr-FR"));
        _room.TimeZone.Returns(TimeZoneInfo.Utc);
        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.GetRoom(Arg.Any<string>()).ReturnsNull();
        _roomsManager.GetRoom(ROOM_ID).Returns(_room);

        _authorizationService = Substitute.For<IAuthorizationService>();
        _authorizationService.GetEffectiveRankAsync(Arg.Any<IContext>(), ROOM_ID, Arg.Any<CancellationToken>())
            .Returns(Rank.RoomOwner);
        _permissionGrantStore = Substitute.For<IPermissionGrantStore>();
        _permissionTargetResolver = Substitute.For<IPermissionTargetResolver>();
        _commandRegistry = Substitute.For<ICommandRegistry>();
        _commandRegistry.Find(Arg.Any<string>()).ReturnsNull();
        _commandRegistry.Commands.Returns([]);
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(NOW);

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns(ROOM_ID);
        _context.Sender.Returns(new User("Owner", Rank.RoomOwner));
        _context.Culture.Returns(CultureInfo.InvariantCulture);

        _command = new GrantCommand(_roomsManager, _authorizationService, _permissionGrantStore,
            _permissionTargetResolver, _commandRegistry, _clockService);
    }

    [Test]
    public void Test_Properties_ShouldBeRoomOwnerOnlyInRoomsAndPrivateMessages()
    {
        // Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.RoomOwner));
        Assert.That(_command.RequiredRankInPrivateMessage, Is.EqualTo(Rank.RoomOwner));
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
        Assert.That(_command.Category, Is.EqualTo("Permissions"));
    }

    [Test]
    [TestCase(false, "alice, %", ExpectedResult = ROOM_ID)]
    [TestCase(true, "alice, %, French Room", ExpectedResult = "frenchroom")]
    [TestCase(true, "alice, %, 3d, otherroom", ExpectedResult = "otherroom")]
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
    [TestCase("")]
    [TestCase("alice")]
    [TestCase(", %")]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenArgumentsAreMissing(string target)
    {
        // Arrange
        _context.Target.Returns(target);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("permissions_grant_help");
        await _permissionGrantStore.DidNotReceiveWithAnyArgs().SaveAsync(default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenBotIsNotInTheRoom()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("alice, %, unknownroom");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("permissions_room_not_found", "unknownroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUnknownTarget_WhenRightCannotBeResolved()
    {
        // Arrange
        _context.Target.Returns("alice, whatever");
        _permissionTargetResolver.Resolve("whatever").ReturnsNull();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("permissions_unknown_target", "whatever");
        await _permissionGrantStore.DidNotReceiveWithAnyArgs().SaveAsync(default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSavePermanentGrant_WhenNoDurationIsGiven()
    {
        // Arrange
        _context.Target.Returns("Alice, %");
        _permissionTargetResolver.Resolve("%").Returns(new PermissionTarget(PermissionGrantKind.Rank, "%"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _permissionGrantStore.Received(1).SaveAsync(
            new PermissionGrant(ROOM_ID, "alice", PermissionGrantKind.Rank, "%", "owner", NOW, null),
            Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("permissions_grant_success", Arg.Any<object[]>());
    }

    [Test]
    [TestCase("3d", 72)]
    [TestCase("1d12h", 36)]
    [TestCase("2 weeks", 336)]
    public async Task Test_RunAsync_ShouldSaveTemporaryGrant_WhenDurationIsGiven(string duration, int expectedHours)
    {
        // Arrange
        _context.Target.Returns($"alice, %, {duration}");
        _permissionTargetResolver.Resolve("%").Returns(new PermissionTarget(PermissionGrantKind.Rank, "%"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _permissionGrantStore.Received(1).SaveAsync(
            Arg.Is<PermissionGrant>(grant => grant.ExpiresAt == NOW.AddHours(expectedHours)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [TestCase("soon")]
    [TestCase("0m")]
    [TestCase("53w")]
    [TestCase("99999999999999999w")]
    public async Task Test_RunAsync_ShouldReplyInvalidDuration_WhenDurationIsInvalidOrTooLong(string duration)
    {
        // Arrange
        _context.Target.Returns($"alice, %, {duration}");
        _permissionTargetResolver.Resolve("%").Returns(new PermissionTarget(PermissionGrantKind.Rank, "%"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("permissions_invalid_duration", duration);
        await _permissionGrantStore.DidNotReceiveWithAnyArgs().SaveAsync(default);
    }

    [Test]
    [TestCase("#")]
    [TestCase("~")]
    [TestCase("♡")]
    [TestCase("*")]
    public async Task Test_RunAsync_ShouldRefuse_WhenRankCannotBeGiven(string symbol)
    {
        // Arrange
        _context.Target.Returns($"alice, {symbol}");
        _permissionTargetResolver.Resolve(symbol).Returns(new PermissionTarget(PermissionGrantKind.Rank, symbol));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("permissions_rank_not_grantable", Arg.Any<object[]>());
        await _permissionGrantStore.DidNotReceiveWithAnyArgs().SaveAsync(default);
    }

    [Test]
    [TestCase(Rank.Driver, false, "Badges", null)]
    [TestCase(Rank.Regular, false, "Badges", "permissions_target_already_allowed")]
    [TestCase(Rank.Leader, false, "Badges", "permissions_target_above_rank")]
    [TestCase(Rank.Driver, true, "Badges", "permissions_target_not_grantable")]
    [TestCase(Rank.RoomOwner, false, "Permissions", "permissions_target_not_grantable")]
    public async Task Test_RunAsync_ShouldOnlyGiveCommands_ThatTheGranterCanRunAndThatAreGrantable(
        Rank requiredRank, bool isWhitelistOnly, string category, string expectedRefusalKey)
    {
        // Arrange
        var target = CreateCommand("somecommand", category, requiredRank, isWhitelistOnly);
        _context.Target.Returns("alice, somecommand");
        _permissionTargetResolver.Resolve("somecommand")
            .Returns(new PermissionTarget(PermissionGrantKind.Command, "somecommand"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        if (expectedRefusalKey == null)
        {
            await _permissionGrantStore.Received(1).SaveAsync(
                Arg.Is<PermissionGrant>(grant => grant.Kind == PermissionGrantKind.Command
                                                 && grant.Target == target.Name),
                Arg.Any<CancellationToken>());
        }
        else
        {
            _context.Received(1).ReplyLocalizedMessage(expectedRefusalKey, Arg.Any<object[]>());
            await _permissionGrantStore.DidNotReceiveWithAnyArgs().SaveAsync(default);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldGiveAdminCommand_WhenGranterIsWhitelisted()
    {
        // Arrange
        CreateCommand("admincommand", "Development", Rank.Admin);
        _context.Target.Returns("alice, admincommand");
        _permissionTargetResolver.Resolve("admincommand")
            .Returns(new PermissionTarget(PermissionGrantKind.Command, "admincommand"));
        _authorizationService.GetEffectiveRankAsync(_context, ROOM_ID, Arg.Any<CancellationToken>())
            .Returns(Rank.Admin);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _permissionGrantStore.Received(1).SaveAsync(Arg.Any<PermissionGrant>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldCheckPrivateMessageRank_WhenItIsHigherThanRoomRank()
    {
        // Arrange
        var target = CreateCommand("pmcommand", "Badges", Rank.Regular);
        target.RequiredRankInPrivateMessage.Returns(Rank.Leader);
        _context.Target.Returns("alice, pmcommand");
        _permissionTargetResolver.Resolve("pmcommand")
            .Returns(new PermissionTarget(PermissionGrantKind.Command, "pmcommand"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("permissions_target_above_rank", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldGiveCategory_WhenGranterCanRunAllItsCommands()
    {
        // Arrange
        ICommand[] commands =
        [
            CreateCommand("add-badge", "Badges", Rank.Driver),
            CreateCommand("badgelist", "Badges", Rank.Regular),
            CreateCommand("debug", "Badges", Rank.Admin, isWhitelistOnly: true)
        ];
        _commandRegistry.Commands.Returns(commands);
        _context.Target.Returns("alice, category:Badges");
        _permissionTargetResolver.Resolve("category:Badges")
            .Returns(new PermissionTarget(PermissionGrantKind.Category, "Badges"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _permissionGrantStore.Received(1).SaveAsync(
            Arg.Is<PermissionGrant>(grant => grant.Kind == PermissionGrantKind.Category && grant.Target == "Badges"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [TestCase(Rank.Leader, "permissions_target_above_rank")]
    [TestCase(Rank.Regular, "permissions_target_already_allowed")]
    public async Task Test_RunAsync_ShouldRefuseCategory_WhenItsCommandsAreAboveGranterOrOpenToAll(
        Rank requiredRank, string expectedRefusalKey)
    {
        // Arrange
        ICommand[] commands =
        [
            CreateCommand("badgelist", "Badges", Rank.Regular),
            CreateCommand("other", "Badges", requiredRank)
        ];
        _commandRegistry.Commands.Returns(commands);
        _context.Target.Returns("alice, Badges");
        _permissionTargetResolver.Resolve("Badges")
            .Returns(new PermissionTarget(PermissionGrantKind.Category, "Badges"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage(expectedRefusalKey, Arg.Any<object[]>());
        await _permissionGrantStore.DidNotReceiveWithAnyArgs().SaveAsync(default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseRoomCulture_WhenInPrivateMessage()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("alice, %, franais");
        _permissionTargetResolver.Resolve("%").Returns(new PermissionTarget(PermissionGrantKind.Rank, "%"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().Culture = _room.Culture;
        await _permissionGrantStore.Received(1).SaveAsync(
            Arg.Is<PermissionGrant>(grant => grant.RoomId == ROOM_ID), Arg.Any<CancellationToken>());
    }

    private ICommand CreateCommand(string name, string category, Rank requiredRank, bool isWhitelistOnly = false)
    {
        var command = Substitute.For<ICommand>();
        command.Name.Returns(name);
        command.Category.Returns(category);
        command.RequiredRank.Returns(requiredRank);
        command.RequiredRankInPrivateMessage.Returns(requiredRank);
        command.IsWhitelistOnly.Returns(isWhitelistOnly);
        _commandRegistry.Find(name).Returns(command);
        return command;
    }
}
