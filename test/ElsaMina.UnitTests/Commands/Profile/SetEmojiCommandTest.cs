using ElsaMina.Commands.Profile;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Profile;

public class SetEmojiCommandTest
{
    private SetEmojiCommand _command;
    private IRoomUserDataService _roomUserDataService;
    private IContext _context;
    private IUser _sender;

    [SetUp]
    public void SetUp()
    {
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _command = new SetEmojiCommand(_roomUserDataService);
        _context = Substitute.For<IContext>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("testuser");
        _context.Sender.Returns(_sender);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetEmoji_WhenCalledFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Target.Returns("😀");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserEmojiAsync("testroom", "testuser", "😀");
        _context.Received(1).ReplyLocalizedMessage("set_emoji_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetEmoji_WhenCalledFromPm()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("testroom, 🎮");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserEmojiAsync("testroom", "testuser", "🎮");
        _context.Received(1).ReplyLocalizedMessage("set_emoji_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearEmoji_WhenClearCommandFromRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Command.Returns("clearemoji");
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserEmojiAsync("testroom", "testuser", string.Empty);
        _context.Received(1).ReplyLocalizedMessage("set_emoji_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearEmoji_WhenClearCommandFromPm()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Command.Returns("clearemoji");
        _context.Target.Returns("testroom");

        await _command.RunAsync(_context);

        await _roomUserDataService.Received(1).SetUserEmojiAsync("testroom", "testuser", string.Empty);
        _context.Received(1).ReplyLocalizedMessage("set_emoji_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenPmWithNoRoomId()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserEmojiAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalid_WhenEmojiIsAsciiOnly()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Target.Returns("abc");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("set_emoji_invalid");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserEmojiAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalid_WhenEmojiIsTooLong()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Target.Returns("😀😀😀😀😀😀😀😀😀");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("set_emoji_invalid");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().SetUserEmojiAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFailure_WhenServiceThrows()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        _context.Target.Returns("😀");
        _roomUserDataService.SetUserEmojiAsync(default, default, default)
            .ThrowsAsyncForAnyArgs(new Exception("db error"));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("set_emoji_failure", "db error");
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
