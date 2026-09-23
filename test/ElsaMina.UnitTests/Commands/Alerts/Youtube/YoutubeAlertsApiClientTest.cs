using System.Net;
using ElsaMina.Commands.Alerts.Youtube;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Alerts.Youtube;

public class YoutubeAlertsApiClientTest
{
    private const string FEED = """
                                <?xml version="1.0" encoding="UTF-8"?>
                                <feed xmlns:yt="http://www.youtube.com/xml/schemas/2015" xmlns="http://www.w3.org/2005/Atom">
                                  <title>Channel</title>
                                  <entry><id>yt:video:abc</id><yt:videoId>abc</yt:videoId></entry>
                                  <entry><id>yt:video:def</id><yt:videoId>def</yt:videoId></entry>
                                </feed>
                                """;

    private IHttpService _httpService;
    private IConfiguration _configuration;
    private YoutubeAlertsApiClient _client;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _configuration.YoutubeApiKey.Returns("key");
        _client = new YoutubeAlertsApiClient(_httpService, _configuration);
    }

    [Test]
    public async Task Test_GetRecentVideoIdsAsync_ShouldParseVideoIds_FromChannelFeed()
    {
        // Arrange
        var response = Substitute.For<IHttpResponse<string>>();
        response.Data.Returns(FEED);
        response.StatusCode.Returns(HttpStatusCode.OK);
        _httpService.SendForStringAsync(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>()).Returns(response);

        // Act
        var videoIds = await _client.GetRecentVideoIdsAsync("UCaaaaaaaaaaaaaaaaaaaaaa");

        // Assert
        Assert.That(videoIds, Is.EqualTo(new[] { "abc", "def" }));
    }

    [TestCase("@SomeHandle", "forHandle", "@SomeHandle")]
    [TestCase("somehandle", "forHandle", "@somehandle")]
    [TestCase("https://www.youtube.com/@SomeHandle", "forHandle", "@SomeHandle")]
    [TestCase("UCaaaaaaaaaaaaaaaaaaaaaa", "id", "UCaaaaaaaaaaaaaaaaaaaaaa")]
    [TestCase("https://youtube.com/channel/UCaaaaaaaaaaaaaaaaaaaaaa", "id", "UCaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Test_ResolveChannelAsync_ShouldQueryChannel_WithParsedInput(string input,
        string expectedParameter, string expectedValue)
    {
        // Arrange
        var response = Substitute.For<IHttpResponse<YoutubeChannelsResponse>>();
        response.Data.Returns(new YoutubeChannelsResponse
        {
            Items = [new YoutubeChannel { Id = "UCx", Snippet = new YoutubeChannelSnippet { CustomUrl = "@somehandle" } }]
        });
        _httpService.SendAsync<YoutubeChannelsResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

        // Act
        var channel = await _client.ResolveChannelAsync(input);

        // Assert
        Assert.That(channel.ChannelId, Is.EqualTo("UCx"));
        Assert.That(channel.ChannelName, Is.EqualTo("somehandle"));
        await _httpService.Received(1).SendAsync<YoutubeChannelsResponse>(
            Arg.Is<HttpRequest>(request => request.QueryParameters[expectedParameter] == expectedValue),
            Arg.Any<CancellationToken>());
    }
}
