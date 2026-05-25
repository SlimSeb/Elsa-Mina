using System.Globalization;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.RandomImages;

[TestFixture]
public class TenorGifCommandTest
{
    private IImageService _imageService;
    private ITemplatesManager _templatesManager;
    private IClockService _clockService;
    private IArcadeEventsService _arcadeEventsService;
    private ITenorCooldownService _cooldownService;
    private IRoom _room;
    private TenorGifCommand _command;

    [SetUp]
    public void SetUp()
    {
        _imageService = Substitute.For<IImageService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _clockService = Substitute.For<IClockService>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _cooldownService = Substitute.For<ITenorCooldownService>();
        _room = Substitute.For<IRoom>();

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<img/>");
        _room.GetParameterValueAsync(Parameter.TenorGifEnabled, Arg.Any<CancellationToken>())
            .Returns("true");
        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _arcadeEventsService.AreGamesMuted(Arg.Any<string>()).Returns(false);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((TimeSpan.Zero, TimeSpan.Zero));

        _command = new TenorGifCommand(_imageService, _templatesManager, _clockService, _arcadeEventsService,
            _cooldownService);
    }

    private IContext MakeContext(string target, string roomId = null, string userId = null)
    {
        roomId ??= Guid.NewGuid().ToString();
        userId ??= Guid.NewGuid().ToString();
        var context = Substitute.For<IContext>();
        var user = Substitute.For<IUser>();
        user.UserId.Returns(userId);
        context.Sender.Returns(user);
        context.Target.Returns(target);
        context.Culture.Returns(CultureInfo.InvariantCulture);
        context.Room.Returns(_room);
        context.RoomId.Returns(roomId);
        return context;
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenTenorGifIsDisabled()
    {
        _room.GetParameterValueAsync(Parameter.TenorGifEnabled, Arg.Any<CancellationToken>())
            .Returns("false");
        var context = MakeContext("https://media.tenor.com/a.gif|200|100");

        await _command.RunAsync(context);

        context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        context.DidNotReceive().Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMutedMessage_WhenGamesAreMutedForEvents()
    {
        _arcadeEventsService.AreGamesMuted(Arg.Any<string>()).Returns(true);
        var context = MakeContext("https://media.tenor.com/a.gif|200|100");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("tenorgif_muted_for_events");
        context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendCooldownMessageAndNotSendGif_WhenRoomCooldownIsLonger()
    {
        var roomRemaining = TimeSpan.FromSeconds(60);
        var userRemaining = TimeSpan.FromSeconds(10);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((roomRemaining, userRemaining));
        var context = MakeContext("https://media.tenor.com/a.gif|200|100");
        context.GetString("tenorgif_room_cooldown", Arg.Any<object[]>()).Returns("room on cooldown");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Is<string>(msg => msg.Contains("room on cooldown")), rankAware: Arg.Any<bool>());
        context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldBypassRoomCooldown_WhenSenderIsWhitelisted()
    {
        var roomRemaining = TimeSpan.FromSeconds(60);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((roomRemaining, TimeSpan.Zero));
        var context = MakeContext("https://media.tenor.com/a.gif|200|100");
        context.IsSenderWhitelisted.Returns(true);

        await _command.RunAsync(context);

        context.DidNotReceive().Reply(Arg.Is<string>(msg => msg.Contains("cooldown")), rankAware: Arg.Any<bool>());
        context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: false);
    }

    [Test]
    public async Task Test_RunAsync_ShouldEnforceUserCooldown_EvenWhenSenderIsWhitelisted()
    {
        var userRemaining = TimeSpan.FromMinutes(5);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((TimeSpan.Zero, userRemaining));
        var context = MakeContext("https://media.tenor.com/a.gif|200|100");
        context.IsSenderWhitelisted.Returns(true);
        context.GetString("tenorgif_user_cooldown", Arg.Any<object[]>()).Returns("user on cooldown");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Is<string>(msg => msg.Contains("user on cooldown")), rankAware: Arg.Any<bool>());
        context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendCooldownMessageAndNotSendGif_WhenUserCooldownIsLonger()
    {
        var roomRemaining = TimeSpan.FromSeconds(10);
        var userRemaining = TimeSpan.FromMinutes(14);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((roomRemaining, userRemaining));
        var context = MakeContext("https://media.tenor.com/a.gif|200|100");
        context.GetString("tenorgif_user_cooldown", Arg.Any<object[]>()).Returns("user on cooldown");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Is<string>(msg => msg.Contains("user on cooldown")), rankAware: Arg.Any<bool>());
        context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldShowRoomCooldownMessage_WhenBothAreEqual()
    {
        var remaining = TimeSpan.FromSeconds(30);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((remaining, remaining));
        var context = MakeContext("https://media.tenor.com/a.gif|200|100");
        context.GetString("tenorgif_room_cooldown", Arg.Any<object[]>()).Returns("room on cooldown");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Is<string>(msg => msg.Contains("room on cooldown")), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsEmpty()
    {
        var context = MakeContext(string.Empty);
        context.GetString(_command.HelpMessageKey).Returns("help text");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidUrl_WhenUrlIsNotTenorCdn()
    {
        var context = MakeContext("https://example.com/image.gif");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("tenorgif_invalid_url");
        context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidUrl_WhenUrlIsHttp()
    {
        var context = MakeContext("http://media.tenor.com/a.gif");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("tenorgif_invalid_url");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidUrl_WhenUrlIsNotAbsolute()
    {
        var context = MakeContext("not-a-url");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("tenorgif_invalid_url");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotSetCooldown_WhenUrlIsInvalid()
    {
        var context = MakeContext("https://example.com/image.gif");

        await _command.RunAsync(context);

        _cooldownService.DidNotReceiveWithAnyArgs().SetCooldown(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseEncodedDimensions_WhenPresentInTarget()
    {
        var context = MakeContext("https://media.tenor.com/a.gif|400|200");

        await _command.RunAsync(context);

        await _imageService.DidNotReceiveWithAnyArgs().GetRemoteImageDimensions(default);
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/RandomImages/TenorGif",
            Arg.Is<TenorGifViewModel>(vm =>
                vm.Url == "https://media.tenor.com/a.gif" &&
                vm.Width == 200 &&
                vm.Height == 100));
    }

    [Test]
    public async Task Test_RunAsync_ShouldFetchDimensionsFromImageService_WhenNotEncodedInTarget()
    {
        var context = MakeContext("https://media.tenor.com/a.gif");
        _imageService.GetRemoteImageDimensions(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((600, 300));

        await _command.RunAsync(context);

        await _imageService.Received(1).GetRemoteImageDimensions(
            "https://media.tenor.com/a.gif", Arg.Any<CancellationToken>());
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/RandomImages/TenorGif",
            Arg.Is<TenorGifViewModel>(vm => vm.Width == 300 && vm.Height == 150));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtml_WithRenderedTemplate()
    {
        var context = MakeContext("https://media.tenor.com/a.gif|200|100");

        await _command.RunAsync(context);

        context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: false);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetCooldown_AfterGifIsSent()
    {
        var now = DateTimeOffset.UtcNow;
        var roomId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        _clockService.CurrentUtcDateTimeOffset.Returns(now);
        var context = MakeContext("https://media.tenor.com/a.gif|200|100", roomId, userId);

        await _command.RunAsync(context);

        Received.InOrder(() =>
        {
            context.ReplyHtml(Arg.Any<string>(), rankAware: false);
            _cooldownService.SetCooldown(roomId, userId, now);
        });
    }
}
