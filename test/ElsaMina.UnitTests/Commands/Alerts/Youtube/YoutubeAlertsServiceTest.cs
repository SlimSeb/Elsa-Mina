using System.Globalization;
using ElsaMina.Commands.Alerts;
using ElsaMina.Commands.Alerts.Youtube;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Alerts.Youtube;

public class YoutubeAlertsServiceTest
{
    private const string CHANNEL_ID = "UCaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly DateTimeOffset NOW = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private IAlertsManager _alertsManager;
    private IYoutubeAlertsApiClient _youtubeApiClient;
    private IClockService _clockService;
    private IRoomsManager _roomsManager;
    private ITemplatesManager _templatesManager;
    private IBot _bot;
    private YoutubeAlertsService _service;

    [SetUp]
    public void SetUp()
    {
        _alertsManager = Substitute.For<IAlertsManager>();
        _youtubeApiClient = Substitute.For<IYoutubeAlertsApiClient>();
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(NOW);
        _roomsManager = Substitute.For<IRoomsManager>();
        foreach (var roomId in new[] { "videoroom", "liveroom" })
        {
            var room = Substitute.For<IRoom>();
            room.Culture.Returns(new CultureInfo("en-US"));
            _roomsManager.GetRoom(roomId).Returns(room);
        }

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");
        _bot = Substitute.For<IBot>();
        _service = new YoutubeAlertsService(_alertsManager, _youtubeApiClient, _clockService, _roomsManager,
            _templatesManager, _bot, TimeSpan.FromMinutes(5));

        _alertsManager.GetAlerts(AlertPlatforms.YOUTUBE, AlertPlatforms.YOUTUBE_LIVE).Returns([
            new AlertSubscription("videoroom", AlertPlatforms.YOUTUBE, CHANNEL_ID, "channel"),
            new AlertSubscription("liveroom", AlertPlatforms.YOUTUBE_LIVE, CHANNEL_ID, "channel")
        ]);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    private void SetFeed(params string[] videoIds)
    {
        _youtubeApiClient.GetRecentVideoIdsAsync(CHANNEL_ID, Arg.Any<CancellationToken>()).Returns(videoIds);
    }

    private void SetVideos(params YoutubeVideo[] videos)
    {
        _youtubeApiClient.GetVideosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var requestedIds = callInfo.Arg<IReadOnlyCollection<string>>();
                return videos.Where(video => requestedIds.Contains(video.Id)).ToList();
            });
    }

    private static YoutubeVideo CreateVideo(string videoId, string liveBroadcastContent,
        DateTimeOffset publishedAt, bool hasLiveDetails = false) => new()
    {
        Id = videoId,
        Snippet = new YoutubeVideoSnippet
        {
            Title = videoId,
            ChannelTitle = "Channel",
            LiveBroadcastContent = liveBroadcastContent,
            PublishedAt = publishedAt
        },
        LiveStreamingDetails = hasLiveDetails
            ? new YoutubeLiveStreamingDetails { ActualStartTime = publishedAt }
            : null
    };

    [Test]
    public async Task Test_PollOnceAsync_ShouldNotAnnounce_WhenPrimingExistingVideos()
    {
        // Arrange
        SetFeed("old");
        SetVideos(CreateVideo("old", "none", NOW.AddMinutes(-1)));

        // Act
        await _service.PollOnceAsync();

        // Assert
        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldAnnounceVideoOnlyInVideoRooms_WhenNewVideoIsPublished()
    {
        // Arrange
        SetFeed("old");
        SetVideos(CreateVideo("old", "none", NOW.AddDays(-3)), CreateVideo("new", "none", NOW.AddMinutes(-2)));
        await _service.PollOnceAsync();
        SetFeed("new", "old");

        // Act
        await _service.PollOnceAsync();

        // Assert
        _bot.Received(1).Say("videoroom", "/addhtmlbox html");
        _bot.DidNotReceive().Say("liveroom", Arg.Any<string>());
        await _templatesManager.Received(1).GetTemplateAsync("Alerts/Youtube/YoutubeAlert",
            Arg.Is<YoutubeAlertViewModel>(viewModel => !viewModel.IsLive && viewModel.VideoId == "new"));
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldAnnounceLiveOnlyInLiveRooms_WhenUpcomingStreamGoesLive()
    {
        // Arrange
        SetFeed("stream");
        SetVideos(CreateVideo("stream", "upcoming", NOW.AddDays(-1)));
        await _service.PollOnceAsync();
        SetVideos(CreateVideo("stream", "live", NOW.AddMinutes(-1), hasLiveDetails: true));

        // Act
        await _service.PollOnceAsync();
        await _service.PollOnceAsync();

        // Assert
        _bot.Received(1).Say("liveroom", "/addhtmlbox html");
        _bot.DidNotReceive().Say("videoroom", Arg.Any<string>());
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldNotAnnounceVideo_WhenItIsAFinishedLiveStream()
    {
        // Arrange
        SetFeed();
        SetVideos(CreateVideo("vod", "none", NOW.AddMinutes(-1), hasLiveDetails: true));
        await _service.PollOnceAsync();
        SetFeed("vod");

        // Act
        await _service.PollOnceAsync();

        // Assert
        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldNotRequestVideoDetailsAgain_WhenVideosAreAlreadyHandled()
    {
        // Arrange
        SetFeed("old");
        SetVideos(CreateVideo("old", "none", NOW.AddDays(-3)));
        await _service.PollOnceAsync();

        // Act
        await _service.PollOnceAsync();

        // Assert
        await _youtubeApiClient.Received(1)
            .GetVideosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }
}
