using ElsaMina.Commands.Profile;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Profile;

public class SetProfileColorCommandTest
{
    private SetProfileColorCommand _command;
    private IRoomUserDataService _roomUserDataService;
    private IContext _context;
    private IUser _sender;

    [SetUp]
    public void SetUp()
    {
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _command = new SetProfileColorCommand(_roomUserDataService);
        _context = Substitute.For<IContext>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("testuser");
        _context.Sender.Returns(_sender);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetBackgroundColor_WhenCalledFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofilecolor");
        _context.Target.Returns("purple");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserBackgroundColorAsync("testroom", "testuser", "#8867aa73");
        _context.Received(1).ReplyLocalizedMessage("set_profile_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetBackgroundColor_WhenCalledFromPm()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Command.Returns("setprofilecolor");
        _context.Target.Returns("testroom, red");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserBackgroundColorAsync("testroom", "testuser", "#aa676773");
        _context.Received(1).ReplyLocalizedMessage("set_profile_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearBackgroundColor_WhenClearCommandFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("removeprofilecolor");
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserBackgroundColorAsync("testroom", "testuser", string.Empty);
        _context.Received(1).ReplyLocalizedMessage("set_profile_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearBackgroundColor_WhenClearCommandFromPm()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Command.Returns("clear-profile-color");
        _context.Target.Returns("testroom");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserBackgroundColorAsync("testroom", "testuser", string.Empty);
        _context.Received(1).ReplyLocalizedMessage("set_profile_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearBackgroundColor_WhenNoColorGivenFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofilecolor");
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserBackgroundColorAsync("testroom", "testuser", string.Empty);
        _context.Received(1).ReplyLocalizedMessage("set_profile_color_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenPmWithNoRoomId()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Command.Returns("setprofilecolor");
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserBackgroundColorAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalid_WhenColorIsUnknown()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofilecolor");
        _context.Target.Returns("unknowncolor");

        await _command.RunAsync(_context);

        _context.ReceivedWithAnyArgs(1).ReplyLocalizedMessage("set_profile_color_invalid");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserBackgroundColorAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFailure_WhenServiceThrows()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("setprofilecolor");
        _context.Target.Returns("blue");
        _roomUserDataService.SetUserBackgroundColorAsync(default, default, default)
            .ThrowsAsyncForAnyArgs(new Exception("db error"));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("set_profile_color_failure", "db error");
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
