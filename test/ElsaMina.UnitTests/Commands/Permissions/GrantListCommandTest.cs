using System.Globalization;
using ElsaMina.Commands.Permissions;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Permissions;

public class GrantListCommandTest
{
    private const string ROOM_ID = "franais";
    private static readonly DateTimeOffset NOW = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private IRoomsManager _roomsManager;
    private IPermissionGrantStore _permissionGrantStore;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private GrantListCommand _command;

    [SetUp]
    public void SetUp()
    {
        var room = Substitute.For<IRoom>();
        room.RoomId.Returns(ROOM_ID);
        room.Name.Returns("Français");
        room.Culture.Returns(new CultureInfo("fr-FR"));
        room.TimeZone.Returns(TimeZoneInfo.Utc);
        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.GetRoom(Arg.Any<string>()).ReturnsNull();
        _roomsManager.GetRoom(ROOM_ID).Returns(room);
        _permissionGrantStore = Substitute.For<IPermissionGrantStore>();
        _permissionGrantStore.GetActiveGrants(Arg.Any<string>()).Returns(Array.Empty<PermissionGrant>());
        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<table></table>");

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns(ROOM_ID);
        _context.Target.Returns(string.Empty);
        _context.Sender.Returns(new User("Staff", Rank.Driver));
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.GetString(Arg.Any<string>()).Returns(callInfo => callInfo.ArgAt<string>(0));
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(callInfo => $"{callInfo.ArgAt<string>(0)}:{string.Join(",", callInfo.ArgAt<object[]>(1))}");

        _command = new GrantListCommand(_roomsManager, _permissionGrantStore, _templatesManager);
    }

    [Test]
    public void Test_Properties_ShouldBeDriverInRoomsAndPrivateMessages()
    {
        // Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
        Assert.That(_command.RequiredRankInPrivateMessage, Is.EqualTo(Rank.Driver));
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    [TestCase(false, "", ExpectedResult = ROOM_ID)]
    [TestCase(true, "otherroom", ExpectedResult = "otherroom")]
    [TestCase(true, "alice, otherroom", ExpectedResult = "otherroom")]
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
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenNoRightWasGiven()
    {
        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("permissions_list_empty", "Français");
        _context.DidNotReceiveWithAnyArgs().SendHtmlTo(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendSortedListPrivately_WhenRightsWereGiven()
    {
        // Arrange
        _permissionGrantStore.GetActiveGrants(ROOM_ID).Returns([
            CreateGrant("bob", PermissionGrantKind.Command, "add-badge", NOW.AddDays(1)),
            CreateGrant("alice", PermissionGrantKind.Rank, "%")
        ]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Permissions/GrantList",
            Arg.Is<GrantListViewModel>(viewModel =>
                viewModel.RoomName == "Français"
                && viewModel.Rows.Select(row => row.UserId).SequenceEqual(new[] { "alice", "bob" })
                && viewModel.Rows[0].Right == "permissions_target_rank:%"
                && viewModel.Rows[0].Expires == "permissions_never_expires"
                && viewModel.Rows[1].Right == "permissions_target_command:add-badge"
                && viewModel.Rows[1].GrantedBy == "owner"));
        _context.Received(1).SendHtmlTo("staff", "<table></table>");
    }

    [Test]
    public async Task Test_RunAsync_ShouldOnlyListRightsOfTheUser_WhenOneIsGiven()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("Alice, franais");
        _permissionGrantStore.GetActiveGrants(ROOM_ID).Returns([
            CreateGrant("bob", PermissionGrantKind.Command, "add-badge"),
            CreateGrant("alice", PermissionGrantKind.Rank, "%")
        ]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Permissions/GrantList",
            Arg.Is<GrantListViewModel>(viewModel => viewModel.Rows.Single().UserId == "alice"));
    }

    private static PermissionGrant CreateGrant(string userId, PermissionGrantKind kind, string target,
        DateTimeOffset? expiresAt = null) =>
        new(ROOM_ID, userId, kind, target, "owner", NOW, expiresAt);
}
