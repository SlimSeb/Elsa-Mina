using System.Globalization;
using System.Net;
using ElsaMina.Commands.Alerts;
using ElsaMina.Commands.Alerts.Twitter;
using ElsaMina.Core;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Alerts.Twitter;

public class TwitterAlertsServiceTest
{
    private IAlertsManager _alertsManager;
    private ITwitterApiClient _twitterApiClient;
    private IRoomsManager _roomsManager;
    private ITemplatesManager _templatesManager;
    private IBot _bot;
    private TwitterAlertsService _service;

    [SetUp]
    public void SetUp()
    {
        _alertsManager = Substitute.For<IAlertsManager>();
        _twitterApiClient = Substitute.For<ITwitterApiClient>();
        _roomsManager = Substitute.For<IRoomsManager>();
        var room = Substitute.For<IRoom>();
        room.Culture.Returns(new CultureInfo("en-US"));
        _roomsManager.GetRoom("room").Returns(room);
        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("tweet");
        _bot = Substitute.For<IBot>();
        _service = new TwitterAlertsService(_alertsManager, _twitterApiClient, _roomsManager, _templatesManager,
            _bot, TimeSpan.FromMinutes(15));

        _alertsManager.GetAlerts(AlertPlatforms.TWITTER)
            .Returns([new AlertSubscription("room", AlertPlatforms.TWITTER, "42", "someone")]);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    private static TwitterTweetsResponse CreateResponse(params string[] tweetIds) => new()
    {
        Data = tweetIds.Select(tweetId => new TwitterTweet { Id = tweetId, Text = $"text {tweetId}" }).ToList(),
        Meta = new TwitterTweetsMeta { NewestId = tweetIds.FirstOrDefault() }
    };

    [Test]
    public async Task Test_PollOnceAsync_ShouldNotAnnounce_WhenPrimingExistingTweets()
    {
        // Arrange
        _twitterApiClient.GetLatestTweetsAsync("42", null, Arg.Any<CancellationToken>())
            .Returns(CreateResponse("10", "9"));

        // Act
        await _service.PollOnceAsync();

        // Assert
        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldAnnounceNewTweetsInChronologicalOrder_WhenTweetsArePosted()
    {
        // Arrange
        _twitterApiClient.GetLatestTweetsAsync("42", null, Arg.Any<CancellationToken>())
            .Returns(CreateResponse("10"));
        _twitterApiClient.GetLatestTweetsAsync("42", "10", Arg.Any<CancellationToken>())
            .Returns(CreateResponse("12", "11"));
        await _service.PollOnceAsync();

        // Act
        await _service.PollOnceAsync();

        // Assert
        Received.InOrder(() =>
        {
            _templatesManager.GetTemplateAsync("Alerts/Twitter/TweetAlert",
                Arg.Is<TweetAlertViewModel>(viewModel => viewModel.TweetId == "11"));
            _templatesManager.GetTemplateAsync("Alerts/Twitter/TweetAlert",
                Arg.Is<TweetAlertViewModel>(viewModel => viewModel.TweetId == "12"));
        });
        _bot.Received(2).Say("room", "/addhtmlbox tweet");
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldUseLastTweetId_OnFollowingPolls()
    {
        // Arrange
        _twitterApiClient.GetLatestTweetsAsync("42", null, Arg.Any<CancellationToken>())
            .Returns(CreateResponse("10"));
        _twitterApiClient.GetLatestTweetsAsync("42", "10", Arg.Any<CancellationToken>())
            .Returns(CreateResponse());
        await _service.PollOnceAsync();

        // Act
        await _service.PollOnceAsync();
        await _service.PollOnceAsync();

        // Assert
        await _twitterApiClient.Received(2).GetLatestTweetsAsync("42", "10", Arg.Any<CancellationToken>());
        _bot.DidNotReceive().Say(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_PollOnceAsync_ShouldStopPolling_WhenRateLimited()
    {
        // Arrange
        _alertsManager.GetAlerts(AlertPlatforms.TWITTER).Returns([
            new AlertSubscription("room", AlertPlatforms.TWITTER, "1", "first"),
            new AlertSubscription("room", AlertPlatforms.TWITTER, "2", "second")
        ]);
        _twitterApiClient.GetLatestTweetsAsync("1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpException(HttpStatusCode.TooManyRequests, "rate limited"));

        // Act
        await _service.PollOnceAsync();

        // Assert
        await _twitterApiClient.DidNotReceive()
            .GetLatestTweetsAsync("2", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
