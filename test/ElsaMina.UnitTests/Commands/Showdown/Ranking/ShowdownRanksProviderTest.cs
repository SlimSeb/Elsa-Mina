using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.System;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Showdown.Ranking;

[TestFixture]
public class ShowdownRanksProviderTest
{
    private const string RANK_RESOURCE_URL =
        "https://play.pokemonshowdown.com/~~showdown/action.php?act=ladderget&user={0}";

    private IHttpService _httpService;
    private ISystemService _systemService;
    private ShowdownRanksProvider _provider;

    private static readonly TimeSpan FAST_RETRY = TimeSpan.FromMilliseconds(1);

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _systemService = Substitute.For<ISystemService>();
        _provider = new ShowdownRanksProvider(_httpService, _systemService, FAST_RETRY);
    }

    [Test]
    public async Task Test_GetRankingDataAsync_ShouldCallGetAsyncWithFormattedUrl()
    {
        var mockResponse = Substitute.For<IHttpResponse<IEnumerable<RankingDataDto>>>();
        mockResponse.Data.Returns([]);
        _httpService
            .SendAsync<IEnumerable<RankingDataDto>>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(mockResponse);

        await _provider.GetRankingDataAsync("testuser");

        await _httpService.Received(1).SendAsync<IEnumerable<RankingDataDto>>(
            Arg.Is<HttpRequest>(request =>
                request.Uri == string.Format(RANK_RESOURCE_URL, "testuser") &&
                request.SkipFirstResponseCharacter),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetRankingDataAsync_ShouldReturnDataFromResponse()
    {
        var expectedRankings = new List<RankingDataDto>
        {
            new() { FormatId = "gen9ou", Elo = 1500, Wins = 10, Losses = 5 },
            new() { FormatId = "gen8ou", Elo = 1400, Wins = 8, Losses = 3 }
        };
        var mockResponse = Substitute.For<IHttpResponse<IEnumerable<RankingDataDto>>>();
        mockResponse.Data.Returns(expectedRankings);
        _httpService
            .SendAsync<IEnumerable<RankingDataDto>>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(mockResponse);

        var result = await _provider.GetRankingDataAsync("testuser");

        Assert.That(result, Is.EquivalentTo(expectedRankings));
    }

    [Test]
    public async Task Test_GetRankingDataAsync_ShouldPassCancellationToken()
    {
        var mockResponse = Substitute.For<IHttpResponse<IEnumerable<RankingDataDto>>>();
        mockResponse.Data.Returns([]);
        _httpService
            .SendAsync<IEnumerable<RankingDataDto>>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(mockResponse);
        using var cts = new CancellationTokenSource();

        await _provider.GetRankingDataAsync("testuser", cts.Token);

        await _httpService.Received(1).SendAsync<IEnumerable<RankingDataDto>>(
            Arg.Any<HttpRequest>(),
            cts.Token);
    }

    [Test]
    public async Task Test_GetRankingDataAsync_ShouldRetryOnFailure_AndSucceedOnSecondAttempt()
    {
        // Arrange
        var mockResponse = Substitute.For<IHttpResponse<IEnumerable<RankingDataDto>>>();
        mockResponse.Data.Returns([new RankingDataDto { FormatId = "gen9ou" }]);

        var callCount = 0;
        _httpService
            .SendAsync<IEnumerable<RankingDataDto>>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount < 2)
                {
                    throw new HttpRequestException("temporary failure");
                }

                return Task.FromResult(mockResponse);
            });

        // Act
        var result = await _provider.GetRankingDataAsync("testuser");

        // Assert
        Assert.That(result, Is.Not.Empty);
        await _httpService.Received(2).SendAsync<IEnumerable<RankingDataDto>>(
            Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetRankingDataAsync_ShouldRetryUpToThreeTimes_AndThrowOnAllFailures()
    {
        // Arrange
        _httpService
            .SendAsync<IEnumerable<RankingDataDto>>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<IHttpResponse<IEnumerable<RankingDataDto>>>>(_ =>
                throw new HttpRequestException("always fails"));

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetRankingDataAsync("testuser"));
        await _httpService.Received(3).SendAsync<IEnumerable<RankingDataDto>>(
            Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetRankingDataAsync_ShouldNotRetry_WhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _httpService
            .SendAsync<IEnumerable<RankingDataDto>>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<IHttpResponse<IEnumerable<RankingDataDto>>>>(_ =>
                throw new OperationCanceledException());

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(() =>
            _provider.GetRankingDataAsync("testuser", cts.Token));
        await _httpService.Received(1).SendAsync<IEnumerable<RankingDataDto>>(
            Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>());
    }
}
