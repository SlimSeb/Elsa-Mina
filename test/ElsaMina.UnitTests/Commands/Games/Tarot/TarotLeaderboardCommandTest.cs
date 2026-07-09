using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class TarotLeaderboardCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private TarotLeaderboardCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new BotDbContext(_dbOptions);
        db.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult("<html>leaderboard</html>"));

        _context = Substitute.For<IContext>();

        _sut = new TarotLeaderboardCommand(_dbContextFactory, _templatesManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_sut.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_sut.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenNoScores()
    {
        await _sut.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("tarot_leaderboard_empty");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderLeaderboard_WhenScoresExist()
    {
        await SeedStatsAsync(("user1", 10));

        await _sut.RunAsync(_context);

        await _templatesManager.Received(1)
            .GetTemplateAsync("Games/Tarot/TarotLeaderboard", Arg.Any<TarotLeaderboardViewModel>());
        _context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderByScoreDescending_WhenNotReverseCommand()
    {
        _context.Command.Returns("tarotleaderboard");
        await SeedStatsAsync(("low", 5), ("high", 30), ("mid", 15));

        var capturedViewModel = await CaptureViewModelAsync();

        var entries = capturedViewModel.Leaderboard;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries[0].UserId, Is.EqualTo("high"));
            Assert.That(entries[1].UserId, Is.EqualTo("mid"));
            Assert.That(entries[2].UserId, Is.EqualTo("low"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderByScoreAscending_WhenReverseCommand()
    {
        _context.Command.Returns("tarotlbreverse");
        await SeedStatsAsync(("low", 5), ("high", 30), ("mid", 15));

        var capturedViewModel = await CaptureViewModelAsync();

        var entries = capturedViewModel.Leaderboard;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries[0].UserId, Is.EqualTo("low"));
            Assert.That(entries[1].UserId, Is.EqualTo("mid"));
            Assert.That(entries[2].UserId, Is.EqualTo("high"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldSelectLowestScorers_WhenReverseCommand()
    {
        _context.Command.Returns("tarotlbreverse");
        var stats = new (string, int)[25];
        for (var i = 0; i < 25; i++)
        {
            stats[i] = ($"user{i}", i);
        }
        await SeedStatsAsync(stats);

        var capturedViewModel = await CaptureViewModelAsync();

        var entries = capturedViewModel.Leaderboard;
        using (Assert.EnterMultipleScope())
        {
            // Lowest 20 scorers (scores 0..19), displayed lowest-first.
            Assert.That(entries[0].TotalScoreHalfPoints, Is.EqualTo(0));
            Assert.That(entries[^1].TotalScoreHalfPoints, Is.EqualTo(19));
            // The top scorers (scores 20..24) are excluded.
            Assert.That(entries.Select(entry => entry.TotalScoreHalfPoints), Has.None.GreaterThan(19));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldLimit20Entries_WhenMoreExist()
    {
        var stats = new (string, int)[25];
        for (var i = 0; i < 25; i++)
        {
            stats[i] = ($"user{i}", i);
        }
        await SeedStatsAsync(stats);

        var capturedViewModel = await CaptureViewModelAsync();

        Assert.That(capturedViewModel.Leaderboard, Has.Count.EqualTo(20));
    }

    private async Task SeedStatsAsync(params (string UserId, int TotalScoreHalfPoints)[] stats)
    {
        await using var db = new BotDbContext(_dbOptions);
        foreach (var stat in stats)
        {
            db.Users.Add(new SavedUser { UserId = stat.UserId, UserName = stat.UserId });
            db.TarotStats.Add(new TarotStats
            {
                UserId = stat.UserId, TotalScoreHalfPoints = stat.TotalScoreHalfPoints
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task<TarotLeaderboardViewModel> CaptureViewModelAsync()
    {
        TarotLeaderboardViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Do<TarotLeaderboardViewModel>(vm => capturedViewModel = vm));

        await _sut.RunAsync(_context);

        Assert.That(capturedViewModel, Is.Not.Null);
        return capturedViewModel;
    }
}
