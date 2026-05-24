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
    private IRoom _room;
    private TenorGifCommand _command;

    [SetUp]
    public void SetUp()
    {
        _imageService = Substitute.For<IImageService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _clockService = Substitute.For<IClockService>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _room = Substitute.For<IRoom>();

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<img/>");
        _room.GetParameterValueAsync(Parameter.TenorGifEnabled, Arg.Any<CancellationToken>())
            .Returns("true");
        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _arcadeEventsService.AreGamesMuted(Arg.Any<string>()).Returns(false);

        _command = new TenorGifCommand(_imageService, _templatesManager, _clockService, _arcadeEventsService);
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
    public async Task Test_RunAsync_ShouldNotSendGif_WhenRoomIsOnCooldown()
    {
        var roomId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        _clockService.CurrentUtcDateTimeOffset.Returns(now);
        var firstContext = MakeContext("https://media.tenor.com/a.gif|200|100", roomId, Guid.NewGuid().ToString());
        await _command.RunAsync(firstContext);
        firstContext.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: false);

        var secondContext = MakeContext("https://media.tenor.com/b.gif|200|100", roomId, Guid.NewGuid().ToString());
        await _command.RunAsync(secondContext);

        secondContext.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotSendGif_WhenUserIsOnCooldown()
    {
        var userId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        _clockService.CurrentUtcDateTimeOffset.Returns(now);
        var firstContext = MakeContext("https://media.tenor.com/a.gif|200|100", Guid.NewGuid().ToString(), userId);
        await _command.RunAsync(firstContext);
        firstContext.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: false);

        _clockService.CurrentUtcDateTimeOffset.Returns(now.AddMinutes(5));
        var secondContext = MakeContext("https://media.tenor.com/b.gif|200|100", Guid.NewGuid().ToString(), userId);
        await _command.RunAsync(secondContext);

        secondContext.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendGif_WhenBothCooldownsHaveExpired()
    {
        var roomId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var start = DateTimeOffset.UtcNow;
        _clockService.CurrentUtcDateTimeOffset.Returns(start);
        var firstContext = MakeContext("https://media.tenor.com/a.gif|200|100", roomId, userId);
        await _command.RunAsync(firstContext);

        _clockService.CurrentUtcDateTimeOffset.Returns(start.AddHours(1));
        var secondContext = MakeContext("https://media.tenor.com/b.gif|200|100", roomId, userId);
        await _command.RunAsync(secondContext);

        secondContext.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: false);
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
}
