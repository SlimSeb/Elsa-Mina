using System.Globalization;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.RandomImages;

[TestFixture]
public class KlipySearchCommandTest
{
    private IKlipyService _klipyService;
    private IConfiguration _configuration;
    private ITemplatesManager _templatesManager;
    private IClockService _clockService;
    private IArcadeEventsService _eventsService;
    private IGifCooldownService _cooldownService;
    private IRoom _room;
    private KlipySearchCommand _command;

    [SetUp]
    public void SetUp()
    {
        _klipyService = Substitute.For<IKlipyService>();
        _configuration = Substitute.For<IConfiguration>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _eventsService = Substitute.For<IArcadeEventsService>();
        _clockService = Substitute.For<IClockService>();
        _cooldownService = Substitute.For<IGifCooldownService>();
        _room = Substitute.For<IRoom>();

        _configuration.Trigger.Returns("-");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<html/>");
        _room.GetParameterValueAsync(Parameter.KlipyGifEnabled, Arg.Any<CancellationToken>())
            .Returns("true");
        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _eventsService.AreGamesMuted(Arg.Any<string>()).Returns(false);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((TimeSpan.Zero, TimeSpan.Zero));

        _command = new KlipySearchCommand(_klipyService, _configuration, _templatesManager, _clockService,
            _eventsService, _cooldownService);
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

    /// <summary>
    /// A search hit whose full variant carries the given dimensions and whose preview is half as wide.
    /// </summary>
    private static GifSearchResult MakeSearchResult(string slug, int fullWidth, int fullHeight) =>
        new(new GifMediaInfo($"https://static.klipy.com/{slug}-xs.gif", fullWidth / 2, fullHeight / 2),
            new GifMediaInfo($"https://static.klipy.com/{slug}-sm.gif", fullWidth, fullHeight));

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeFalse()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.False);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenKlipyGifIsDisabled()
    {
        _room.GetParameterValueAsync(Parameter.KlipyGifEnabled, Arg.Any<CancellationToken>())
            .Returns("false");
        var context = MakeContext("cats");

        await _command.RunAsync(context);

        await _klipyService.DidNotReceiveWithAnyArgs().SearchAsync(default, default);
        context.DidNotReceive().SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        context.DidNotReceive().Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyErrorMessage_WhenArcadeGamesAreMuted()
    {
        _eventsService.AreGamesMuted(Arg.Any<string>()).Returns(true);
        var context = MakeContext("cats");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("klipygif_muted_for_events");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendRoomCooldownMessageAndNotSendGif_WhenRoomCooldownIsLonger()
    {
        var roomRemaining = TimeSpan.FromSeconds(60);
        var userRemaining = TimeSpan.FromSeconds(10);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((roomRemaining, userRemaining));
        var context = MakeContext("cats");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("klipysearch_room_cooldown", Arg.Any<object[]>());
        context.DidNotReceive().SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldBypassRoomCooldown_WhenSenderIsWhitelisted()
    {
        var context = MakeContext("cats");
        context.IsSenderWhitelisted.Returns(true);
        var roomRemaining = TimeSpan.FromSeconds(60);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((roomRemaining, TimeSpan.Zero));
        _klipyService.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([MakeSearchResult("a", 200, 100)]);

        await _command.RunAsync(context);

        context.DidNotReceive().ReplyLocalizedMessage("klipysearch_room_cooldown", Arg.Any<object[]>());
        context.Received(1).SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotEnforceUserCooldown_WhenSenderIsWhitelisted()
    {
        const string userId = "user";
        const string roomId = "room";
        _klipyService.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([MakeSearchResult("a", 200, 100)]);

        var now = DateTimeOffset.UtcNow;
        _clockService.CurrentUtcDateTimeOffset.Returns(now);
        _cooldownService.GetRemainingCooldowns(roomId, userId, now)
            .Returns((TimeSpan.FromMilliseconds(1), TimeSpan.FromMinutes(5)));
        var context = MakeContext("cats", roomId, userId);
        context.IsSenderWhitelisted.Returns(true);

        await _command.RunAsync(context);

        context.DidNotReceive().ReplyLocalizedMessage("klipysearch_user_cooldown", Arg.Any<object[]>());
        context.Received(1).SendHtmlTo(userId, Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendUserCooldownMessageAndNotSendGif_WhenUserCooldownIsLonger()
    {
        var roomRemaining = TimeSpan.FromSeconds(10);
        var userRemaining = TimeSpan.FromMinutes(14);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((roomRemaining, userRemaining));
        var context = MakeContext("cats");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("klipysearch_user_cooldown", Arg.Any<object[]>());
        context.DidNotReceive().SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldShowRoomCooldownMessage_WhenBothAreEqual()
    {
        var remaining = TimeSpan.FromSeconds(30);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((remaining, remaining));
        var context = MakeContext("cats");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("klipysearch_room_cooldown", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsEmpty()
    {
        var context = MakeContext(string.Empty);
        context.GetString(_command.HelpMessageKey).Returns("help text");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        await _klipyService.DidNotReceiveWithAnyArgs().SearchAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenKlipyReturnsNoResults()
    {
        var context = MakeContext("cats");
        _klipyService.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("random_image_error");
        context.DidNotReceive().SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotSetCooldown_WhenKlipyReturnsNoResults()
    {
        var context = MakeContext("cats");
        _klipyService.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _command.RunAsync(context);

        _cooldownService.DidNotReceiveWithAnyArgs().SetCooldown(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendPrivateHtml_WhenKlipyReturnsResults()
    {
        var context = MakeContext("cats");
        _klipyService.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([MakeSearchResult("a", 200, 100)]);

        await _command.RunAsync(context);

        context.Received(1).SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldFetchGifsWithCorrectSearchTerm()
    {
        var context = MakeContext("  funny cats  ");
        _klipyService.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _command.RunAsync(context);

        await _klipyService.Received(1).SearchAsync(
            "funny cats", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WithCorrectViewModel()
    {
        var context = MakeContext("dogs");
        _klipyService.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                MakeSearchResult("a", 400, 200),
                MakeSearchResult("b", 300, 150)
            ]);

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/RandomImages/KlipySearch",
            Arg.Is<KlipySearchViewModel>(vm =>
                vm.Gifs.Count == 2 &&
                vm.Gifs[0].PreviewUrl == "https://static.klipy.com/a-xs.gif" &&
                vm.Gifs[0].FullUrl == "https://static.klipy.com/a-sm.gif" &&
                vm.Gifs[0].FullWidth == 400 &&
                vm.Gifs[0].FullHeight == 200 &&
                vm.Trigger == "-"));
    }
}
