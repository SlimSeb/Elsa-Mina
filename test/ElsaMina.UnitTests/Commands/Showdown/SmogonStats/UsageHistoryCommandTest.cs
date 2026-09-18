using System.Globalization;
using ElsaMina.Cloud;
using ElsaMina.Commands.Showdown.SmogonStats;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Smogon;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Showdown.SmogonStats;

[TestFixture]
public class UsageHistoryCommandTest
{
    private static readonly DateTime CURRENT_DATE = new(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private ISmogonUsageDataProvider _smogonUsageDataProvider;
    private IFileSharingService _fileSharingService;
    private IClockService _clockService;
    private IContext _context;
    private UsageHistoryCommand _command;

    [SetUp]
    public void SetUp()
    {
        _smogonUsageDataProvider = Substitute.For<ISmogonUsageDataProvider>();
        _fileSharingService = Substitute.For<IFileSharingService>();
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTime.Returns(CURRENT_DATE);

        _context = Substitute.For<IContext>();
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.GetString(Arg.Any<string>()).Returns("{0}");
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>()).Returns(string.Empty);

        _command = new UsageHistoryCommand(_smogonUsageDataProvider, _fileSharingService, _clockService);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetHasOnlyOnePart()
    {
        // Arrange
        _context.Target.Returns("Great Tusk");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString(_command.HelpMessageKey);
        await _smogonUsageDataProvider.DidNotReceive().GetUsageRankingAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Level>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString(_command.HelpMessageKey);
    }

    [Test]
    public async Task Test_RunAsync_ShouldRequestTwelveMonthsEndingLastMonth_WhenNoCountGiven()
    {
        // Arrange
        _context.Target.Returns("Great Tusk, gen9ou");
        MockRanking();
        MockUpload("https://cdn.example.com/usagegraph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _smogonUsageDataProvider.Received(12).GetUsageRankingAsync(Arg.Any<string>(), "gen9ou",
            Arg.Any<Level>(), Arg.Any<CancellationToken>());
        await _smogonUsageDataProvider.Received(1).GetUsageRankingAsync("2025-05", "gen9ou",
            Arg.Any<Level>(), Arg.Any<CancellationToken>());
        await _smogonUsageDataProvider.Received(1).GetUsageRankingAsync("2024-06", "gen9ou",
            Arg.Any<Level>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldHonourRequestedMonthCount_WhenCountGiven()
    {
        // Arrange
        _context.Target.Returns("Great Tusk, gen9ou, 3");
        MockRanking();
        MockUpload("https://cdn.example.com/usagegraph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _smogonUsageDataProvider.Received(3).GetUsageRankingAsync(Arg.Any<string>(), "gen9ou",
            Arg.Any<Level>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldClampMonthCount_WhenCountIsOutOfRange()
    {
        // Arrange
        _context.Target.Returns("Great Tusk, gen9ou, 500");
        MockRanking();
        MockUpload("https://cdn.example.com/usagegraph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _smogonUsageDataProvider.Received(36).GetUsageRankingAsync(Arg.Any<string>(), "gen9ou",
            Arg.Any<Level>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseRequestedLevel_WhenLevelGiven()
    {
        // Arrange
        _context.Target.Returns("Great Tusk, gen9ou, 3, Low");
        MockRanking();
        MockUpload("https://cdn.example.com/usagegraph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _smogonUsageDataProvider.Received(3).GetUsageRankingAsync(Arg.Any<string>(), "gen9ou",
            Level.Low, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotEnoughData_WhenPokemonIsAbsentFromRankings()
    {
        // Arrange
        _context.Target.Returns("Missingno, gen9ou");
        MockRanking();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("usage_history_not_enough_data", "Missingno", "gen9ou");
        await _fileSharingService.DidNotReceive().CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotEnoughData_WhenOnlyOneMonthHasData()
    {
        // Arrange
        _context.Target.Returns("Great Tusk, gen9ou");
        _smogonUsageDataProvider.GetUsageRankingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Level>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<string>(0) == "2025-05"
                ? RankingWithGreatTusk(30)
                : throw new HttpRequestException("404"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("usage_history_not_enough_data", "Great Tusk", "gen9ou");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSkipUnavailableMonths_WhenSomeRequestsFail()
    {
        // Arrange - Smogon answers 404 for months a format was not published in
        _context.Target.Returns("Great Tusk, gen9ou");
        _smogonUsageDataProvider.GetUsageRankingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Level>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<string>(0) is "2025-04" or "2025-05"
                ? RankingWithGreatTusk(30)
                : throw new HttpRequestException("404"));
        MockUpload("https://cdn.example.com/usagegraph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(
            Arg.Is<string>(html => html.Contains("https://cdn.example.com/usagegraph.png") && html.Contains("<img")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldMatchPokemonIgnoringPunctuation_WhenNameIsNormalized()
    {
        // Arrange
        _context.Target.Returns("ogerponwellspring, gen9ou");
        _smogonUsageDataProvider.GetUsageRankingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Level>(),
                Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SmogonUsageRankingEntryDto>>(_ =>
                [new SmogonUsageRankingEntryDto(1, "Ogerpon-Wellspring", 23.9, 417331)]);
        MockUpload("https://cdn.example.com/usagegraph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _fileSharingService.Received(1).CreateFileAsync(
            Arg.Any<byte[]>(),
            Arg.Is<string>(name => name.StartsWith("usagegraphs/usagegraph-ogerponwellspring-gen9ou-")),
            description: "Usage history for Ogerpon-Wellspring in gen9ou",
            mimeType: "image/jpeg",
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUploadFailed_WhenFileSharingReturnsNull()
    {
        // Arrange
        _context.Target.Returns("Great Tusk, gen9ou");
        MockRanking();
        MockUpload(null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("usage_history_upload_failed");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtmlWithImageTag_WhenUploadSucceeds()
    {
        // Arrange
        _context.Target.Returns("Great Tusk, gen9ou");
        MockRanking();
        MockUpload("https://cdn.example.com/usagegraph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(
            Arg.Is<string>(html => html.Contains("https://cdn.example.com/usagegraph.png") && html.Contains("<img")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldGetLocalizedChartLabels_WhenGeneratingGraph()
    {
        // Arrange
        _context.Target.Returns("Great Tusk, gen9ou");
        MockRanking();
        MockUpload("https://cdn.example.com/usagegraph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("usage_history_chart_title", "Great Tusk", "gen9ou");
        _context.Received(1).GetString("usage_history_chart_x_label");
        _context.Received(1).GetString("usage_history_chart_y_label");
        _context.Received(1).GetString("usage_history_peak");
        _context.Received(1).GetString("usage_history_latest");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallHandleErrorAsync_WhenUploadThrows()
    {
        // Arrange
        _context.Target.Returns("Great Tusk, gen9ou");
        MockRanking();
        var exception = new InvalidOperationException("upload exploded");
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw exception);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _context.Received(1).HandleErrorAsync(exception, Arg.Any<CancellationToken>());
    }

    [Test]
    public void Test_RequiredRank_And_HelpMessageKey_ShouldMatchContract()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
            Assert.That(_command.HelpMessageKey, Is.EqualTo("usage_history_help"));
            Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
        }
    }

    private static IReadOnlyList<SmogonUsageRankingEntryDto> RankingWithGreatTusk(double usagePercentage)
    {
        return [new SmogonUsageRankingEntryDto(1, "Great Tusk", usagePercentage, 588614)];
    }

    private void MockRanking()
    {
        var monthIndex = 0;
        _smogonUsageDataProvider.GetUsageRankingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Level>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => RankingWithGreatTusk(20 + Interlocked.Increment(ref monthIndex)));
    }

    private void MockUpload(string url)
    {
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(url);
    }
}
