using System.Globalization;
using ElsaMina.Commands.Showdown.Ladder.EloHistory;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.FileSharing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Showdown.Ladder.EloHistory;

public class LadderGraphCommandTest
{
    private IBotDbContextFactory _dbContextFactory;
    private IFileSharingService _fileSharingService;
    private IContext _context;
    private LadderGraphCommand _command;
    private DbContextOptions<BotDbContext> _dbOptions;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new BotDbContext(_dbOptions);
        dbContext.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _fileSharingService = Substitute.For<IFileSharingService>();
        _context = Substitute.For<IContext>();
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.Command.Returns("ladderhistory");
        _context.GetString(Arg.Any<string>()).Returns("{0}");
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>()).Returns(string.Empty);

        _command = new LadderGraphCommand(_dbContextFactory, _fileSharingService);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetHasOnlyOnePart()
    {
        // Arrange
        _context.Target.Returns("gen9ou");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString(_command.HelpMessageKey);
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
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
    public async Task Test_RunAsync_ShouldReplyNotEnoughData_WhenFewerThanTwoSnapshots()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");
        await SeedSnapshotsAsync("gen9ou", "alice", 1);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("ladder_graph_not_enough_data", "alice");
        await _fileSharingService.DidNotReceive()
            .CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotEnoughData_WhenNoSnapshotsExist()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("ladder_graph_not_enough_data", "alice");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUploadFailed_WhenFileSharingReturnsNull()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");
        await SeedSnapshotsAsync("gen9ou", "alice", 3);
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("ladder_graph_upload_failed");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtmlWithImageTag_WhenUploadSucceeds()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");
        await SeedSnapshotsAsync("gen9ou", "alice", 3);
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/elograph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(
            Arg.Is<string>(html => html.Contains("https://cdn.example.com/elograph.png")
                                   && html.Contains("<img")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeFormatAndUserId_WhenQuerying()
    {
        // Arrange
        _context.Target.Returns("Gen 9 OU, Alice Test");
        await SeedSnapshotsAsync("gen9ou", "alicetest", 3);
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/elograph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert — if normalization works, the snapshots are found and the graph is generated
        _context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUploadWithCorrectMetadata_WhenGeneratingGraph()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");
        await SeedSnapshotsAsync("gen9ou", "alice", 3);
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/elograph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _fileSharingService.Received(1).CreateFileAsync(
            Arg.Is<byte[]>(b => b.Length > 0),
            Arg.Is<string>(name => name.StartsWith("elographs/elograph-alice-gen9ou-")),
            description: "ELO history for alice in gen9ou",
            mimeType: "image/png",
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldGetLocalizedChartLabels_WhenGeneratingGraph()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");
        await SeedSnapshotsAsync("gen9ou", "alice", 3);
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/elograph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("ladder_graph_chart_title", "alice", "gen9ou");
        _context.Received(1).GetString("ladder_graph_chart_x_label");
        _context.Received(1).GetString("ladder_graph_chart_y_label");
        _context.Received(1).GetString("ladder_graph_trend_slope");
        _context.Received(1).GetString("ladder_graph_trend_r_squared");
    }

    [Test]
    public async Task Test_RunAsync_ShouldGenerateAndUploadGraph_WhenCommandIsElotrend()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");
        _context.Command.Returns("elotrend");
        await SeedSnapshotsAsync("gen9ou", "alice", 3);
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/elograph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(
            Arg.Is<string>(html => html.Contains("https://cdn.example.com/elograph.png") && html.Contains("<img")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldGenerateAndUploadGraph_WhenCommandIsLaddertrend()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");
        _context.Command.Returns("laddertrend");
        await SeedSnapshotsAsync("gen9ou", "alice", 3);
        _fileSharingService.CreateFileAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/elograph.png");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(
            Arg.Is<string>(html => html.Contains("https://cdn.example.com/elograph.png") && html.Contains("<img")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallHandleErrorAsync_WhenExceptionOccurs()
    {
        // Arrange
        _context.Target.Returns("gen9ou, alice");
        var exception = new Exception("db failure");
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(exception);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _context.Received(1).HandleErrorAsync(exception, Arg.Any<CancellationToken>());
    }

    [Test]
    public void Test_RequiredRank_And_HelpMessageKey_ShouldMatchContract()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
            Assert.That(_command.HelpMessageKey, Is.EqualTo("ladder_graph_help"));
            Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
        });
    }

    private async Task SeedSnapshotsAsync(string format, string userId, int count)
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        var baseDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
        {
            dbContext.LadderEloSnapshots.Add(new LadderEloSnapshot
            {
                UserId = userId,
                Format = format,
                Elo = 1500 + i * 10,
                RecordedAt = baseDate.AddHours(i)
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
