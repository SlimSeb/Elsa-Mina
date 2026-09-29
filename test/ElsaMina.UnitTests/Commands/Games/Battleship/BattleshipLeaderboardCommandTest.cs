using ElsaMina.Commands.Games.Battleship;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Battleship;

public class BattleshipLeaderboardCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private BattleshipLeaderboardCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<html>\nleaderboard</html>");

        _context = Substitute.For<IContext>();

        _command = new BattleshipLeaderboardCommand(_dbContextFactory, _templatesManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenNoRatingsExist()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("battleship_leaderboard_empty");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassLeaderboardSortedByRatingDescending()
    {
        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            setupContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
            setupContext.Users.Add(new SavedUser { UserId = "bob", UserName = "Bob" });
            setupContext.BattleshipRatings.Add(new BattleshipRating { UserId = "alice", Rating = 900 });
            setupContext.BattleshipRatings.Add(new BattleshipRating { UserId = "bob", Rating = 1200 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Games/Battleship/BattleshipLeaderboard",
            Arg.Is<BattleshipLeaderboardViewModel>(viewModel =>
                viewModel.Leaderboard.Count == 2 &&
                viewModel.Leaderboard[0].UserId == "bob" &&
                viewModel.Leaderboard[1].UserId == "alice"));
        _context.Received(1).ReplyHtml("<html>leaderboard</html>", rankAware: true);
    }
}
