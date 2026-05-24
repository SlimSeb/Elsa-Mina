using ElsaMina.Commands.Arcade.Points;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Points;

public class LeaderboardCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private LeaderboardCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new BotDbContext(_dbOptions);
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => new BotDbContext(_dbOptions));

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<html>template</html>");

        _context = Substitute.For<IContext>();

        _command = new LeaderboardCommand(_dbContextFactory, _templatesManager);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        var command = new LeaderboardCommand(_dbContextFactory, _templatesManager);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("classement"));
            Assert.That(command.RequiredRank, Is.EqualTo(Rank.Voiced));
            Assert.That(command.HelpMessageKey, Is.EqualTo("leaderboard_help"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenNoPointsExist()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("leaderboard_empty");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenAllUsersHaveZeroPoints()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 0 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 0 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("leaderboard_empty");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDisplayLeaderboard_WhenUsersHavePoints()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 15 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 25 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user3", Points = 10 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/Leaderboard",
            Arg.Is<LeaderboardViewModel>(vm =>
                vm.Leaderboard.Count == 3 &&
                vm.Leaderboard["user1"] == 15 &&
                vm.Leaderboard["user2"] == 25 &&
                vm.Leaderboard["user3"] == 10
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldExcludeZeroPoints_WhenFetchingLeaderboard()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 15 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 0 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user3", Points = 10 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/Leaderboard",
            Arg.Is<LeaderboardViewModel>(vm =>
                vm.Leaderboard.Count == 2 &&
                !vm.Leaderboard.ContainsKey("user2")
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHtmlBox_WhenSuccessful()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 10 });
            await setupContext.SaveChangesAsync();
        }

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<b>Leaderboard</b>");

        await _command.RunAsync(_context);

        _context.Received(1).Reply("/addhtmlbox <b>Leaderboard</b>");
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderByPointsDescending_WhenDisplayingLeaderboard()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 5 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 20 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user3", Points = 15 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/Leaderboard",
            Arg.Is<LeaderboardViewModel>(vm =>
                vm.Leaderboard.Count == 3 &&
                vm.Leaderboard.Keys.ElementAt(0) == "user2" &&
                vm.Leaderboard.Keys.ElementAt(1) == "user3" &&
                vm.Leaderboard.Keys.ElementAt(2) == "user1"
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleDecimalPoints_WhenDisplaying()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 10.5 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 7.25 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/Leaderboard",
            Arg.Is<LeaderboardViewModel>(vm =>
                vm.Leaderboard["user1"] == 10.5 &&
                vm.Leaderboard["user2"] == 7.25
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleError_WhenDatabaseFails()
    {
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BotDbContext>(new Exception("Database error")));

        var action = async () => await _command.RunAsync(_context);

        Assert.That(action, Throws.TypeOf<Exception>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCancellationToken_WhenProvided()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 10 });
            await setupContext.SaveChangesAsync();
        }

        var cancellationToken = CancellationToken.None;

        await _command.RunAsync(_context, cancellationToken);

        await _dbContextFactory.Received(1).CreateDbContextAsync(cancellationToken);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleSingleUser_WhenOnlyOneUserHasPoints()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "onlyuser", Points = 100 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/Leaderboard",
            Arg.Is<LeaderboardViewModel>(vm =>
                vm.Leaderboard.Count == 1 &&
                vm.Leaderboard["onlyuser"] == 100
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleNegativePoints_WhenDisplaying()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = -5 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 10 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/Leaderboard",
            Arg.Is<LeaderboardViewModel>(vm =>
                vm.Leaderboard.Count == 1 &&
                !vm.Leaderboard.ContainsKey("user1")
            )
        );
    }
}
