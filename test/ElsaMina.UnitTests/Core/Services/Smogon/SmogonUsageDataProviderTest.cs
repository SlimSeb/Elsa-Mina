using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Smogon;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Smogon;

[TestFixture]
public class SmogonUsageDataProviderTest
{
    private const string SAMPLE_USAGE_TABLE =
        """
         Total battles: 1086636
         Avg. weight/team: 0.063
         + ---- + ------------------ + --------- + ------ + ------- + ------ + ------- +
         | Rank | Pokemon            | Usage %   | Raw    | %       | Real   | %       |
         + ---- + ------------------ + --------- + ------ + ------- + ------ + ------- +
         | 1    | Great Tusk         | 34.36666% | 588614 | 27.084% | 462714 | 26.926% |
         | 2    | Kingambit          | 26.03128% | 444937 | 20.473% | 299818 | 17.447% |
         | 3    | Ogerpon-Wellspring | 23.95341% | 417331 | 19.203% | 314628 | 18.308% |
         + ---- + ------------------ + --------- + ------ + ------- + ------ + ------- +
        """;

    private IHttpService _httpService;
    private SmogonUsageDataProvider _provider;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _provider = new SmogonUsageDataProvider(_httpService);
    }

    [Test]
    public async Task Test_GetUsageRankingAsync_ShouldRequestTextEndpoint_WhenCalled()
    {
        // Arrange
        MockResponse(SAMPLE_USAGE_TABLE);

        // Act
        await _provider.GetUsageRankingAsync("2025-01", "gen9uu", Level.High);

        // Assert
        await _httpService.Received(1).SendForStringAsync(
            Arg.Is<HttpRequest>(request =>
                request.Uri == "https://www.smogon.com/stats/2025-01/gen9uu-1630.txt"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetUsageRankingAsync_ShouldUseWiderCutoff_WhenFormatIsHighTraffic()
    {
        // Arrange
        MockResponse(SAMPLE_USAGE_TABLE);

        // Act
        await _provider.GetUsageRankingAsync("2025-01", "gen9ou", Level.High);

        // Assert
        await _httpService.Received(1).SendForStringAsync(
            Arg.Is<HttpRequest>(request =>
                request.Uri == "https://www.smogon.com/stats/2025-01/gen9ou-1695.txt"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetUsageRankingAsync_ShouldParseEveryDataRow_WhenTableIsWellFormed()
    {
        // Arrange
        MockResponse(SAMPLE_USAGE_TABLE);

        // Act
        var result = await _provider.GetUsageRankingAsync("2025-01", "gen9ou", Level.VeryHigh);

        // Assert
        Assert.That(result, Has.Count.EqualTo(3));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result[0].Rank, Is.EqualTo(1));
            Assert.That(result[0].PokemonName, Is.EqualTo("Great Tusk"));
            Assert.That(result[0].UsagePercentage, Is.EqualTo(34.36666).Within(1e-6));
            Assert.That(result[0].RawCount, Is.EqualTo(588614));
            Assert.That(result[2].PokemonName, Is.EqualTo("Ogerpon-Wellspring"));
            Assert.That(result[2].Rank, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Test_GetUsageRankingAsync_ShouldSkipHeaderAndSeparatorRows_WhenParsing()
    {
        // Arrange
        MockResponse(SAMPLE_USAGE_TABLE);

        // Act
        var result = await _provider.GetUsageRankingAsync("2025-01", "gen9ou", Level.VeryHigh);

        // Assert
        Assert.That(result.Select(entry => entry.PokemonName), Does.Not.Contain("Pokemon"));
    }

    [Test]
    public async Task Test_GetUsageRankingAsync_ShouldReturnEmpty_WhenContentIsEmpty()
    {
        // Arrange
        MockResponse(string.Empty);

        // Act
        var result = await _provider.GetUsageRankingAsync("2025-01", "gen9ou", Level.VeryHigh);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Test_GetUsageRankingAsync_ShouldPassCancellationToken_WhenCalled()
    {
        // Arrange
        MockResponse(SAMPLE_USAGE_TABLE);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _provider.GetUsageRankingAsync("2025-01", "gen9ou", Level.VeryHigh, cancellationTokenSource.Token);

        // Assert
        await _httpService.Received(1).SendForStringAsync(Arg.Any<HttpRequest>(), cancellationTokenSource.Token);
    }

    private void MockResponse(string content)
    {
        var response = Substitute.For<IHttpResponse<string>>();
        response.Data.Returns(content);
        _httpService.SendForStringAsync(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);
    }
}
