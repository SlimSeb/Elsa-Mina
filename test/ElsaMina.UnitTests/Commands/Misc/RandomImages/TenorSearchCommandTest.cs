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
public class TenorSearchCommandTest
{
    private ITenorService _tenorService;
    private IConfiguration _configuration;
    private ITemplatesManager _templatesManager;
    private IClockService _clockService;
    private IArcadeEventsService _eventsService;
    private ITenorCooldownService _cooldownService;
    private IRoom _room;
    private TenorSearchCommand _command;

    [SetUp]
    public void SetUp()
    {
        _tenorService = Substitute.For<ITenorService>();
        _configuration = Substitute.For<IConfiguration>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _eventsService = Substitute.For<IArcadeEventsService>();
        _clockService = Substitute.For<IClockService>();
        _cooldownService = Substitute.For<ITenorCooldownService>();
        _room = Substitute.For<IRoom>();

        _configuration.Trigger.Returns("-");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<html/>");
        _room.GetParameterValueAsync(Parameter.TenorGifEnabled, Arg.Any<CancellationToken>())
            .Returns("true");
        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _eventsService.AreGamesMuted(Arg.Any<string>()).Returns(false);
        _cooldownService.GetRemainingCooldowns(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns((TimeSpan.Zero, TimeSpan.Zero));

        _command = new TenorSearchCommand(_tenorService, _configuration, _templatesManager, _clockService,
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
    public async Task Test_RunAsync_ShouldDoNothing_WhenTenorGifIsDisabled()
    {
        _room.GetParameterValueAsync(Parameter.TenorGifEnabled, Arg.Any<CancellationToken>())
            .Returns("false");
        var context = MakeContext("cats");

        await _command.RunAsync(context);

        await _tenorService.DidNotReceiveWithAnyArgs().GetMultipleMediaAsync(default, default, default);
        context.DidNotReceive().SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        context.DidNotReceive().Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyErrorMessage_WhenArcadeGamesAreMuted()
    {
        _eventsService.AreGamesMuted(Arg.Any<string>()).Returns(true);
        var context = MakeContext("cats");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("tenorgif_muted_for_events");
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

        context.Received(1).ReplyLocalizedMessage("tenorsearch_room_cooldown", Arg.Any<object[]>());
        context.DidNotReceive().SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
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

        context.Received(1).ReplyLocalizedMessage("tenorsearch_user_cooldown", Arg.Any<object[]>());
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

        context.Received(1).ReplyLocalizedMessage("tenorsearch_room_cooldown", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsEmpty()
    {
        var context = MakeContext(string.Empty);
        context.GetString(_command.HelpMessageKey).Returns("help text");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        await _tenorService.DidNotReceiveWithAnyArgs().GetMultipleMediaAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenTenorReturnsNoResults()
    {
        var context = MakeContext("cats");
        _tenorService.GetMultipleMediaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("random_image_error");
        context.DidNotReceive().SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotSetCooldown_WhenTenorReturnsNoResults()
    {
        var context = MakeContext("cats");
        _tenorService.GetMultipleMediaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await _command.RunAsync(context);

        _cooldownService.DidNotReceiveWithAnyArgs().SetCooldown(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendPrivateHtml_WhenTenorReturnsResults()
    {
        var context = MakeContext("cats");
        _tenorService.GetMultipleMediaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([new TenorMediaInfo("https://media.tenor.com/a.gif", 200, 100)]);

        await _command.RunAsync(context);

        context.Received(1).SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldFetchGifsWithCorrectSearchTerm()
    {
        var context = MakeContext("  funny cats  ");
        _tenorService.GetMultipleMediaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await _command.RunAsync(context);

        await _tenorService.Received(1).GetMultipleMediaAsync(
            "funny cats", "gif", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WithCorrectViewModel()
    {
        var context = MakeContext("dogs");
        _tenorService.GetMultipleMediaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([
                new TenorMediaInfo("https://media.tenor.com/a.gif", 400, 200),
                new TenorMediaInfo("https://media.tenor.com/b.gif", 300, 150)
            ]);

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/RandomImages/TenorSearch",
            Arg.Is<TenorSearchViewModel>(vm =>
                vm.Gifs.Count == 2 &&
                vm.Gifs[0].Url == "https://media.tenor.com/a.gif" &&
                vm.Gifs[0].OriginalWidth == 400 &&
                vm.Gifs[0].OriginalHeight == 200 &&
                vm.Trigger == "-"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetCooldown_AfterGifIsSent()
    {
        var now = DateTimeOffset.UtcNow;
        var roomId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        _clockService.CurrentUtcDateTimeOffset.Returns(now);
        var context = MakeContext("cats", roomId, userId);
        _tenorService.GetMultipleMediaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([new TenorMediaInfo("https://media.tenor.com/a.gif", 200, 100)]);

        await _command.RunAsync(context);

        Received.InOrder(() =>
        {
            context.SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
            _cooldownService.SetCooldown(roomId, userId, now);
        });
    }
}
