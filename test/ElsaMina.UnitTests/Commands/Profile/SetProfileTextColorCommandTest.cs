using ElsaMina.Commands.Profile;
using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Profile;

public class SetProfileTextColorCommandTest
{
    private SetProfileTextColorCommand _command;
    private IRoomUserDataService _roomUserDataService;
    private IEditProfilePanelService _editProfilePanelService;
    private IContext _context;
    private IUser _sender;

    [SetUp]
    public void SetUp()
    {
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _editProfilePanelService = Substitute.For<IEditProfilePanelService>();
        _command = new SetProfileTextColorCommand(_roomUserDataService, _editProfilePanelService);
        _context = Substitute.For<IContext>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("testuser");
        _context.Sender.Returns(_sender);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetTextColor_WhenCalledFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofiletextcolor");
        _context.Target.Returns("Yellow");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserTextColorAsync("testroom", "testuser", "#e0d060");
        _context.Received(1).ReplyLocalizedMessage("set_profile_text_color_success");
        await _editProfilePanelService.Received(1).SendPanelAsync(_context, "testroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetTextColor_WhenCalledFromPm()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Command.Returns("setprofiletextcolor");
        _context.Target.Returns("testroom, teal");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserTextColorAsync("testroom", "testuser", "#6ad0d0");
        _context.Received(1).ReplyLocalizedMessage("set_profile_text_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearTextColor_WhenClearCommandFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("clearprofiletextcolor");
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserTextColorAsync("testroom", "testuser", string.Empty);
        _context.Received(1).ReplyLocalizedMessage("set_profile_text_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearTextColor_WhenClearCommandFromPm()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Command.Returns("clear-profile-text-color");
        _context.Target.Returns("testroom");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserTextColorAsync("testroom", "testuser", string.Empty);
        _context.Received(1).ReplyLocalizedMessage("set_profile_text_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearTextColor_WhenNoColorGivenFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofiletextcolor");
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserTextColorAsync("testroom", "testuser", string.Empty);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenPmWithNoRoomId()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Command.Returns("setprofiletextcolor");
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserTextColorAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalid_WhenColorIsUnknown()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofiletextcolor");
        _context.Target.Returns("chartreuse");

        await _command.RunAsync(_context);

        _context.ReceivedWithAnyArgs(1).ReplyLocalizedMessage("set_profile_text_color_invalid");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserTextColorAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalid_WhenColorIsGradientOnly()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofiletextcolor");
        _context.Target.Returns("rainbow");

        await _command.RunAsync(_context);

        _context.ReceivedWithAnyArgs(1).ReplyLocalizedMessage("set_profile_text_color_invalid");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserTextColorAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFailure_WhenServiceThrows()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofiletextcolor");
        _context.Target.Returns("blue");
        _roomUserDataService.SetUserTextColorAsync(default, default, default)
            .ThrowsAsyncForAnyArgs(new Exception("db error"));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("set_profile_text_color_failure", "db error");
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
