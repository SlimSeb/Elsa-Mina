using System.Globalization;
using ElsaMina.Commands.RoomDashboard;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.RoomDashboard;

[TestFixture]
public class ShowRoomDashboardTest
{
    private IContext _context;
    private IRoomsManager _roomsManager;
    private IRoomDashboardService _roomDashboardService;
    private ShowRoomDashboard _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _roomDashboardService = Substitute.For<IRoomDashboardService>();
        _command = new ShowRoomDashboard(_roomsManager, _roomDashboardService);

        _context.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));
        _context.HasSufficientRankInRoom(Arg.Any<string>(), Arg.Any<Rank>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_ShowRoomDashboard_ShouldDeny_WhenUserHasInsufficientRank()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom");
        _roomsManager.GetRoom("testroom").Returns(room);
        _context.HasSufficientRankInRoom("testroom", Rank.Driver, Arg.Any<CancellationToken>())
            .Returns(false);

        await _command.RunAsync(_context);

        await _roomDashboardService.DidNotReceive()
            .SendDashboardPageAsync(Arg.Any<IContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_ShowRoomDashboard_ShouldRenderPage_WhenUserIsRoomStaff()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom");
        _roomsManager.GetRoom("testroom").Returns(room);
        _context.HasSufficientRankInRoom("testroom", Rank.Driver, Arg.Any<CancellationToken>())
            .Returns(true);

        await _command.RunAsync(_context);

        await _roomDashboardService.Received(1)
            .SendDashboardPageAsync(_context, "testroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomDoesntExist_WhenRoomIsNotFound()
    {
        _context.Target.Returns("unknownroom");
        _roomsManager.GetRoom("unknownroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("dashboard_room_doesnt_exist", "unknownroom");
        await _roomDashboardService.DidNotReceive()
            .SendDashboardPageAsync(Arg.Any<IContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseFallbackRoomId_WhenTargetIsEmpty()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("  ");
        _context.RoomId.Returns("defaultroom");
        _roomsManager.GetRoom("defaultroom").Returns(room);

        await _command.RunAsync(_context);

        _roomsManager.Received(1).GetRoom("defaultroom");
        await _roomDashboardService.Received(1)
            .SendDashboardPageAsync(_context, "defaultroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomDoesntExist_WhenTargetAndRoomIdAreEmpty()
    {
        _context.Target.Returns("  ");
        _context.RoomId.Returns((string)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("dashboard_room_doesnt_exist", string.Empty);
        await _roomDashboardService.DidNotReceive()
            .SendDashboardPageAsync(Arg.Any<IContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTrimmedLowercaseTarget_WhenTargetIsProvided()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("  TestRoom  ");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        _roomsManager.Received(1).GetRoom("testroom");
        await _roomDashboardService.Received(1)
            .SendDashboardPageAsync(_context, "testroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetContextCultureToRoomCulture_WhenContextIsPrivateMessage()
    {
        var room = Substitute.For<IRoom>();
        var roomCulture = CultureInfo.GetCultureInfo("fr-FR");
        room.Name.Returns("Test Room");
        room.Culture.Returns(roomCulture);
        _context.Target.Returns("testroom");
        _context.IsPrivateMessage.Returns(true);
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        Assert.That(_context.Culture, Is.EqualTo(roomCulture));
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotSetContextCulture_WhenContextIsNotPrivateMessage()
    {
        var room = Substitute.For<IRoom>();
        var roomCulture = CultureInfo.GetCultureInfo("fr-FR");
        room.Name.Returns("Test Room");
        room.Culture.Returns(roomCulture);
        _context.Target.Returns("testroom");
        _context.IsPrivateMessage.Returns(false);
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        _context.DidNotReceiveWithAnyArgs().Culture = Arg.Any<CultureInfo>();
    }
}
