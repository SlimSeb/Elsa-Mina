using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.Core.Services.Formats;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Showdown.Ranking;

public class BestRankingProviderTest
{
    private IShowdownRanksProvider _showdownRanksProvider;
    private IFormatsManager _formatsManager;
    private BestRankingProvider _provider;

    [SetUp]
    public void SetUp()
    {
        _showdownRanksProvider = Substitute.For<IShowdownRanksProvider>();
        _formatsManager = Substitute.For<IFormatsManager>();
        _provider = new BestRankingProvider(_showdownRanksProvider, _formatsManager);
    }

    [Test]
    public async Task Test_GetBestRankingAsync_ShouldReturnHighestEloRankingWithCleanFormat_WhenUserHasRankings()
    {
        // Arrange
        var lowRanking = new RankingDataDto { FormatId = "gen9ou", Elo = 1200 };
        var highRanking = new RankingDataDto { FormatId = "gen9ubers", Elo = 1500 };
        _showdownRanksProvider.GetRankingDataAsync("alice", Arg.Any<CancellationToken>())
            .Returns([lowRanking, highRanking]);
        _formatsManager.GetCleanFormat("gen9ubers").Returns("[Gen 9] Ubers");

        // Act
        var result = await _provider.GetBestRankingAsync("alice");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.SameAs(highRanking));
            Assert.That(result.FormatId, Is.EqualTo("[Gen 9] Ubers"));
            Assert.That(lowRanking.FormatId, Is.EqualTo("gen9ou"));
        }
    }

    [Test]
    public async Task Test_GetBestRankingAsync_ShouldReturnNull_WhenRankingDataIsNull()
    {
        // Arrange
        _showdownRanksProvider.GetRankingDataAsync("alice", Arg.Any<CancellationToken>())
            .Returns((IEnumerable<RankingDataDto>)null);

        // Act
        var result = await _provider.GetBestRankingAsync("alice");

        // Assert
        Assert.That(result, Is.Null);
        _formatsManager.DidNotReceiveWithAnyArgs().GetCleanFormat(default);
    }

    [Test]
    public async Task Test_GetBestRankingAsync_ShouldReturnNull_WhenUserHasNoRankings()
    {
        // Arrange
        _showdownRanksProvider.GetRankingDataAsync("alice", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RankingDataDto>());

        // Act
        var result = await _provider.GetBestRankingAsync("alice");

        // Assert
        Assert.That(result, Is.Null);
        _formatsManager.DidNotReceiveWithAnyArgs().GetCleanFormat(default);
    }

    [Test]
    public async Task Test_GetBestRankingAsync_ShouldPassCancellationToken_WhenFetchingRankings()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _provider.GetBestRankingAsync("alice", cancellationTokenSource.Token);

        // Assert
        await _showdownRanksProvider.Received(1).GetRankingDataAsync("alice", cancellationTokenSource.Token);
    }
}
