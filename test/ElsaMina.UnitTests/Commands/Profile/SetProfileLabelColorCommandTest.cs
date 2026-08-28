using ElsaMina.Commands.Profile;
using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Profile;

public class SetProfileLabelColorCommandTest
{
    private SetProfileLabelColorCommand _command;
    private IRoomUserDataService _roomUserDataService;
    private IEditProfilePanelService _editProfilePanelService;
    private IContext _context;
    private IUser _sender;

    [SetUp]
    public void SetUp()
    {
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _editProfilePanelService = Substitute.For<IEditProfilePanelService>();
        _command = new SetProfileLabelColorCommand(_roomUserDataService, _editProfilePanelService);
        _context = Substitute.For<IContext>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("testuser");
        _context.Sender.Returns(_sender);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetLabelColor_WhenCalledFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofilelabelcolor");
        _context.Target.Returns("Yellow");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserLabelColorAsync("testroom", "testuser", "#e0d060");
        _context.Received(1).ReplyLocalizedMessage("set_profile_label_color_success");
        await _editProfilePanelService.Received(1).SendPanelAsync(_context, "testroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetLabelColor_WhenCalledFromPm()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Command.Returns("setprofilelabelcolor");
        _context.Target.Returns("testroom, teal");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserLabelColorAsync("testroom", "testuser", "#6ad0d0");
        _context.Received(1).ReplyLocalizedMessage("set_profile_label_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearLabelColor_WhenClearCommandFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("clearprofilelabelcolor");
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserLabelColorAsync("testroom", "testuser", string.Empty);
        _context.Received(1).ReplyLocalizedMessage("set_profile_label_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearLabelColor_WhenClearCommandFromPm()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Command.Returns("clear-profile-label-color");
        _context.Target.Returns("testroom");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserLabelColorAsync("testroom", "testuser", string.Empty);
        _context.Received(1).ReplyLocalizedMessage("set_profile_label_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearLabelColor_WhenNoColorGivenFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofilelabelcolor");
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserLabelColorAsync("testroom", "testuser", string.Empty);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenPmWithNoRoomId()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Command.Returns("setprofilelabelcolor");
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserLabelColorAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalid_WhenColorIsUnknown()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofilelabelcolor");
        _context.Target.Returns("chartreuse");

        await _command.RunAsync(_context);

        _context.ReceivedWithAnyArgs(1).ReplyLocalizedMessage("set_profile_label_color_invalid");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserLabelColorAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalid_WhenColorIsGradientOnly()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofilelabelcolor");
        _context.Target.Returns("rainbow");

        await _command.RunAsync(_context);

        _context.ReceivedWithAnyArgs(1).ReplyLocalizedMessage("set_profile_label_color_invalid");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserLabelColorAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFailure_WhenServiceThrows()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofilelabelcolor");
        _context.Target.Returns("blue");
        _roomUserDataService.SetUserLabelColorAsync(default, default, default)
            .ThrowsAsyncForAnyArgs(new Exception("db error"));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("set_profile_label_color_failure", "db error");
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }
}
