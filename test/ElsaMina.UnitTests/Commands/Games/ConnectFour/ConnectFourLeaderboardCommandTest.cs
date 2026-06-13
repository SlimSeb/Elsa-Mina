using ElsaMina.Commands.Games.ConnectFour;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.ConnectFour;

public class ConnectFourLeaderboardCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private ConnectFourLeaderboardCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => new BotDbContext(_dbOptions));

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<html>leaderboard</html>");

        _context = Substitute.For<IContext>();

        _command = new ConnectFourLeaderboardCommand(_dbContextFactory, _templatesManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenNoRatingsExist()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("c4_leaderboard_empty");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WhenRatingsExist()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "alice", Rating = 1100 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Games/ConnectFour/ConnectFourLeaderboard",
            Arg.Any<ConnectFourLeaderboardViewModel>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassLeaderboardSortedByRatingDescending()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
            setupContext.Users.Add(new SavedUser { UserId = "bob", UserName = "Bob" });
            setupContext.Users.Add(new SavedUser { UserId = "carol", UserName = "Carol" });
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "alice", Rating = 900 });
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "bob", Rating = 1200 });
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "carol", Rating = 1050 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Games/ConnectFour/ConnectFourLeaderboard",
            Arg.Is<ConnectFourLeaderboardViewModel>(vm =>
                vm.Leaderboard[0].UserId == "bob" &&
                vm.Leaderboard[1].UserId == "carol" &&
                vm.Leaderboard[2].UserId == "alice"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldLimitTo20Entries()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            for (var i = 0; i < 25; i++)
            {
                setupContext.Users.Add(new SavedUser { UserId = $"user{i}", UserName = $"User{i}" });
                setupContext.ConnectFourRatings.Add(new ConnectFourRating
                {
                    UserId = $"user{i}", Rating = 1000 + i
                });
            }
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Games/ConnectFour/ConnectFourLeaderboard",
            Arg.Is<ConnectFourLeaderboardViewModel>(vm => vm.Leaderboard.Count == 20));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtml_WhenTemplateRendered()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
            setupContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "alice", Rating = 1000 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtml("<html>leaderboard</html>", rankAware: true);
    }
}
