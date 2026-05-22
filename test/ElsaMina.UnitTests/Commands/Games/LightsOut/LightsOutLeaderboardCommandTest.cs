using ElsaMina.Commands.Games.LightsOut;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.LightsOut;

[TestFixture]
public class LightsOutLeaderboardCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private LightsOutLeaderboardCommand _sut;

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

        _sut = new LightsOutLeaderboardCommand(_dbContextFactory, _templatesManager);
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

        _context.Received(1).ReplyRankAwareLocalizedMessage("lo_leaderboard_empty");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderLeaderboard_WhenScoresExist()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.LightsOutScores.Add(new LightsOutScore
            {
                UserId = "user1", Level = 5, BestMoves = 10, TotalStars = 12
            });
            await db.SaveChangesAsync();
        }

        await _sut.RunAsync(_context);

        await _templatesManager.Received(1)
            .GetTemplateAsync("Games/LightsOut/LightsOutLeaderboard", Arg.Any<LightsOutLeaderboardViewModel>());
        _context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderByLevelDescThenTotalStarsDescThenBestMovesAsc()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            db.LightsOutScores.AddRange(
                new LightsOutScore { UserId = "a", Level = 3, BestMoves = 5, TotalStars = 10 },
                new LightsOutScore { UserId = "b", Level = 5, BestMoves = 8, TotalStars = 20 },
                new LightsOutScore { UserId = "c", Level = 5, BestMoves = 6, TotalStars = 25 },
                new LightsOutScore { UserId = "d", Level = 5, BestMoves = 6, TotalStars = 25 }
            );
            await db.SaveChangesAsync();
        }

        LightsOutLeaderboardViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Do<LightsOutLeaderboardViewModel>(vm => capturedViewModel = vm));

        await _sut.RunAsync(_context);

        Assert.That(capturedViewModel, Is.Not.Null);
        var entries = capturedViewModel.Leaderboard;
        Assert.That(entries[0].UserId, Is.EqualTo("c").Or.EqualTo("d")); // level 5, 25 stars, 6 moves
        Assert.That(entries[^1].UserId, Is.EqualTo("a")); // lowest level
    }

    [Test]
    public async Task Test_RunAsync_ShouldLimit20Entries_WhenMoreExist()
    {
        await using (var db = new BotDbContext(_dbOptions))
        {
            for (var i = 0; i < 25; i++)
            {
                db.LightsOutScores.Add(new LightsOutScore
                {
                    UserId = $"user{i}", Level = i + 1, BestMoves = 5, TotalStars = i
                });
            }
            await db.SaveChangesAsync();
        }

        LightsOutLeaderboardViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Do<LightsOutLeaderboardViewModel>(vm => capturedViewModel = vm));

        await _sut.RunAsync(_context);

        Assert.That(capturedViewModel!.Leaderboard, Has.Count.EqualTo(20));
    }
}
