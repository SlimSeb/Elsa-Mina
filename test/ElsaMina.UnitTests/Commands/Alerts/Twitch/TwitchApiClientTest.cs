using System.Net;
using ElsaMina.Commands;
using ElsaMina.Commands.Alerts;
using ElsaMina.Commands.Alerts.Twitch;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Http;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Alerts.Twitch;

public class TwitchApiClientTest
{
    private static readonly string[] FIRST_USER_ID = ["1"];

    private static readonly DateTimeOffset NOW = new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero);

    private IHttpService _httpService;
    private ICommandsConfiguration _configuration;
    private IClockService _clockService;
    private TwitchApiClient _client;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<ICommandsConfiguration>();
        _configuration.TwitchClientId.Returns("client-id");
        _configuration.TwitchClientSecret.Returns("client-secret");
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(NOW);
        _httpService.SendAsync<TwitchTokenResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(Response(new TwitchTokenResponse { AccessToken = "token", ExpiresIn = 3600 }));
        _client = new TwitchApiClient(_httpService, _configuration, _clockService);
    }

    private static HttpResponse<T> Response<T>(T data)
    {
        return new HttpResponse<T> { Data = data };
    }

    private void SetUpUser(string login, string id)
    {
        _httpService.SendAsync<TwitchUsersResponse>(
                Arg.Is<HttpRequest>(request => request.QueryParameters["login"] == login),
                Arg.Any<CancellationToken>())
            .Returns(Response(new TwitchUsersResponse { Data = [new TwitchUser { Id = id, Login = login }] }));
    }

    [TestCase("id", "secret", true)]
    [TestCase("", "secret", false)]
    [TestCase("id", " ", false)]
    [TestCase(null, null, false)]
    public void Test_IsConfigured_ShouldRequireClientIdAndSecret(string clientId, string clientSecret,
        bool expected)
    {
        // Arrange
        _configuration.TwitchClientId.Returns(clientId);
        _configuration.TwitchClientSecret.Returns(clientSecret);

        // Assert
        Assert.That(_client.IsConfigured, Is.EqualTo(expected));
    }

    [TestCase("twitch", true)]
    [TestCase("youtube", false)]
    public void Test_CanResolve_ShouldOnlyAcceptTwitch(string platform, bool expected)
    {
        Assert.That(_client.CanResolve(platform), Is.EqualTo(expected));
    }

    [TestCase("SomeStreamer")]
    [TestCase("@SomeStreamer")]
    [TestCase("twitch.tv/SomeStreamer")]
    [TestCase("https://www.twitch.tv/SomeStreamer/")]
    public async Task Test_ResolveChannelAsync_ShouldReturnChannel_WhenInputMatchesALogin(string input)
    {
        // Arrange
        SetUpUser("somestreamer", "123");

        // Act
        var channel = await _client.ResolveChannelAsync(input);

        // Assert
        Assert.That(channel, Is.EqualTo(new AlertChannel("123", "somestreamer")));
    }

    [TestCase(null)]
    [TestCase("not a login")]
    [TestCase("https://youtube.com/someone")]
    public async Task Test_ResolveChannelAsync_ShouldReturnNull_WhenInputIsNotALogin(string input)
    {
        // Act
        var channel = await _client.ResolveChannelAsync(input);

        // Assert
        Assert.That(channel, Is.Null);
        await _httpService.DidNotReceiveWithAnyArgs().SendAsync<TwitchTokenResponse>(default);
    }

    [Test]
    public async Task Test_ResolveChannelAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        _httpService.SendAsync<TwitchUsersResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(Response(new TwitchUsersResponse { Data = [] }));

        // Act
        var channel = await _client.ResolveChannelAsync("ghost");

        // Assert
        Assert.That(channel, Is.Null);
    }

    [Test]
    public async Task Test_ResolveChannelAsync_ShouldSendAuthorizationHeaders()
    {
        // Arrange
        HttpRequest usersRequest = null;
        _httpService.SendAsync<TwitchUsersResponse>(Arg.Do<HttpRequest>(request => usersRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(Response(new TwitchUsersResponse()));
        HttpRequest tokenRequest = null;
        _httpService.SendAsync<TwitchTokenResponse>(Arg.Do<HttpRequest>(request => tokenRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(Response(new TwitchTokenResponse { AccessToken = "token", ExpiresIn = 3600 }));

        // Act
        await _client.ResolveChannelAsync("streamer");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokenRequest.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(tokenRequest.Uri, Is.EqualTo("https://id.twitch.tv/oauth2/token"));
            Assert.That(usersRequest.Headers["Client-Id"], Is.EqualTo("client-id"));
            Assert.That(usersRequest.Headers["Authorization"], Is.EqualTo("Bearer token"));
        }
    }

    [Test]
    public async Task Test_ResolveChannelAsync_ShouldReuseToken_WhenItHasNotExpired()
    {
        // Arrange
        SetUpUser("streamer", "1");

        // Act
        await _client.ResolveChannelAsync("streamer");
        _clockService.CurrentUtcDateTimeOffset.Returns(NOW.AddMinutes(30));
        await _client.ResolveChannelAsync("streamer");

        // Assert
        await _httpService.Received(1).SendAsync<TwitchTokenResponse>(Arg.Any<HttpRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_ResolveChannelAsync_ShouldRequestNewToken_WhenTokenIsAboutToExpire()
    {
        // Arrange
        SetUpUser("streamer", "1");

        // Act
        await _client.ResolveChannelAsync("streamer");
        _clockService.CurrentUtcDateTimeOffset.Returns(NOW.AddMinutes(56));
        await _client.ResolveChannelAsync("streamer");

        // Assert
        await _httpService.Received(2).SendAsync<TwitchTokenResponse>(Arg.Any<HttpRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_ResolveChannelAsync_ShouldRetryWithNewToken_WhenTokenIsRejected()
    {
        // Arrange
        var calls = 0;
        _httpService.SendAsync<TwitchUsersResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls++;
                return calls == 1
                    ? throw new HttpException(HttpStatusCode.Unauthorized, "revoked")
                    : Task.FromResult<IHttpResponse<TwitchUsersResponse>>(Response(new TwitchUsersResponse
                        { Data = [new TwitchUser { Id = "1", Login = "streamer" }] }));
            });

        // Act
        var channel = await _client.ResolveChannelAsync("streamer");

        // Assert
        Assert.That(channel, Is.EqualTo(new AlertChannel("1", "streamer")));
        await _httpService.Received(2).SendAsync<TwitchTokenResponse>(Arg.Any<HttpRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void Test_ResolveChannelAsync_ShouldThrow_WhenTwitchReturnsNoToken()
    {
        // Arrange
        _httpService.SendAsync<TwitchTokenResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(Response(new TwitchTokenResponse()));

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _client.ResolveChannelAsync("streamer"));
    }

    [Test]
    public void Test_ResolveChannelAsync_ShouldNotRetry_WhenErrorIsNotUnauthorized()
    {
        // Arrange
        _httpService.SendAsync<TwitchUsersResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpException(HttpStatusCode.InternalServerError, "oops"));

        // Act & Assert
        Assert.ThrowsAsync<HttpException>(() => _client.ResolveChannelAsync("streamer"));
    }

    [Test]
    public async Task Test_GetLiveStreamsAsync_ShouldQueryUsersInChunksOfOneHundred()
    {
        // Arrange
        var userIds = Enumerable.Range(1, 150).Select(id => id.ToString()).ToList();
        var requests = new List<HttpRequest>();
        _httpService.SendAsync<TwitchStreamsResponse>(Arg.Do<HttpRequest>(requests.Add), Arg.Any<CancellationToken>())
            .Returns(
                Response(new TwitchStreamsResponse { Data = [new TwitchStream { UserId = "1" }] }),
                Response(new TwitchStreamsResponse { Data = null }));

        // Act
        var streams = await _client.GetLiveStreamsAsync(userIds);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests, Has.Count.EqualTo(2));
            Assert.That(requests[0].Uri, Does.StartWith("https://api.twitch.tv/helix/streams?user_id=1&user_id=2&"));
            Assert.That(requests[0].Uri, Does.EndWith("user_id=100&first=100"));
            Assert.That(requests[1].Uri, Does.StartWith("https://api.twitch.tv/helix/streams?user_id=101&"));
            Assert.That(streams.Select(stream => stream.UserId), Is.EqualTo(FIRST_USER_ID));
        }
    }

    [Test]
    public async Task Test_GetLiveStreamsAsync_ShouldReturnEmpty_WhenNoUserIds()
    {
        // Act
        var streams = await _client.GetLiveStreamsAsync([]);

        // Assert
        Assert.That(streams, Is.Empty);
    }
}
