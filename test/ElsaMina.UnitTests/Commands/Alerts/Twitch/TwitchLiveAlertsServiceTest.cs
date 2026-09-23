using System.Globalization;
using ElsaMina.Commands.Alerts;
using ElsaMina.Commands.Alerts.Twitch;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Alerts.Twitch;

public class TwitchLiveAlertsServiceTest
{
    private static readonly DateTimeOffset NOW = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private IAlertsManager _alertsManager;
    private ITwitchApiClient _twitchApiClient;
    private IClockService _clockService;
    private IRoomsManager _roomsManager;
    private ITemplatesManager _templatesManager;
    private IBot _bot;
    private TwitchLiveAlertsService _service;

    [SetUp]
    public void SetUp()
    {
        _alertsManager = Substitute.For<IAlertsManager>();
        _twitchApiClient = Substitute.For<ITwitchApiClient>();
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(NOW);
        _roomsManager = Substitute.For<IRoomsManager>();
        var room = Substitute.For<IRoom>();
        room.Culture.Returns(new CultureInfo("en-US"));
        _roomsManager.GetRoom("room").Returns(room);
        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<b>live</b>");
        _bot = Substitute.For<IBot>();
        _service = new TwitchLiveAlertsService(_alertsManager, _twitchApiClient, _clockService, _roomsManager,
            _templatesManager, _bot, TimeSpan.FromMinutes(2));

        _alertsManager.GetAlerts(AlertPlatforms.TWITCH)
            .Returns([new AlertSubscription("room", AlertPlatforms.TWITCH, "42", "streamer")]);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    private void SetLiveStreams(params TwitchStream[] streams)
    {
        _twitchApiClient.GetLiveStreamsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(streams);
    }

    private static TwitchStream CreateStream(string streamId, DateTimeOffset startedAt) => new()
    {
        Id = streamId,
        UserId = "42",
        UserLogin = "streamer",
        UserName = "Streamer",
        Title = "Title",
        StartedAt = startedAt
    };

    [Test]
    public async Task Test_PollOnceAsync_ShouldAnnounce_WhenStreamGoesLive()
    {
        // Arrange
        SetLiveStreams();
        await _service.PollOnceAsync();
        SetLiveStreams(CreateStream("s1", NOW.AddMinutes(-1)));

        // Act
        await _service.PollOnceAsync();

        // Assert
        _bot.Received(1).Say("room", "/addhtmlbox <b>live</b>");
        await _templatesManager.Received(1).GetTemplateAsync("Alerts/Twitch/TwitchLiveAlert",
            Arg.Is<TwitchLiveAlertViewModel>(viewModel => viewModel.ChannelDisplayName == "Streamer"));
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldNotAnnounceTwice_WhenStreamIsStillLive()
    {
        // Arrange
        SetLiveStreams(CreateStream("s1", NOW.AddMinutes(-1)));
        await _service.PollOnceAsync();

        // Act
        await _service.PollOnceAsync();

        // Assert
        _bot.Received(1).Say("room", Arg.Any<string>());
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldNotAnnounce_WhenStreamStartedLongBeforeFirstPoll()
    {
        // Arrange
        SetLiveStreams(CreateStream("s1", NOW.AddHours(-2)));

        // Act
        await _service.PollOnceAsync();

        // Assert
        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldAnnounceAgain_WhenNewStreamStarts()
    {
        // Arrange
        SetLiveStreams(CreateStream("s1", NOW.AddMinutes(-1)));
        await _service.PollOnceAsync();
        SetLiveStreams(CreateStream("s2", NOW.AddMinutes(-1)));

        // Act
        await _service.PollOnceAsync();

        // Assert
        _bot.Received(2).Say("room", Arg.Any<string>());
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldNotCallApi_WhenThereAreNoAlerts()
    {
        // Arrange
        _alertsManager.GetAlerts(AlertPlatforms.TWITCH).Returns([]);

        // Act
        await _service.PollOnceAsync();

        // Assert
        await _twitchApiClient.DidNotReceiveWithAnyArgs().GetLiveStreamsAsync(default, default);
    }
}
