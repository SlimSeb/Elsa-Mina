using ElsaMina.Commands.Arcade.Points;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Points;

public class RemovePointsCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private RemovePointsCommand _command;

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

        _command = new RemovePointsCommand(_dbContextFactory, _templatesManager);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        var command = new RemovePointsCommand(_dbContextFactory, _templatesManager);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("removepoints"));
            Assert.That(command.RequiredRank, Is.EqualTo(Rank.Voiced));
            Assert.That(command.HelpMessageKey, Is.EqualTo("remove_points_help"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenTargetHasNoComma()
    {
        _context.Target.Returns("user1");
        _context.GetString("remove_points_help").Returns("Help message");

        await _command.RunAsync(_context);

        _context.Received(1).GetString("remove_points_help");
        _context.Received(1).Reply("Help message");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenPointsIsNotANumber()
    {
        _context.Target.Returns("user1,notanumber");
        _context.GetString("remove_points_help").Returns("Help message");

        await _command.RunAsync(_context);

        _context.Received(1).GetString("remove_points_help");
        _context.Received(1).Reply("Help message");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUserNotFound_WhenUserDoesNotExist()
    {
        _context.Target.Returns("nonexistent,5");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("remove_points_user_not_found", "nonexistent");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRemovePointsFromUser_WhenUserExists()
    {
        var existingUser = new UserPoints { Id = "existinguser", Points = 10.0 };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(existingUser);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("existinguser,3");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var updatedUser = await assertContext.UserPoints.FindAsync("existinguser");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser.Points, Is.EqualTo(7.0));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotGoBelowZero_WhenRemovingMorePointsThanAvailable()
    {
        var existingUser = new UserPoints { Id = "user1", Points = 5.0 };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(existingUser);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user1,10");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var updatedUser = await assertContext.UserPoints.FindAsync("user1");
        Assert.That(updatedUser.Points, Is.EqualTo(0.0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleDecimalPoints_WhenRemoving()
    {
        var existingUser = new UserPoints { Id = "user1", Points = 10.5 };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(existingUser);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user1,2.5");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var updatedUser = await assertContext.UserPoints.FindAsync("user1");
        Assert.That(updatedUser.Points, Is.EqualTo(8.0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeUserId_WhenTargetContainsSpecialCharacters()
    {
        var existingUser = new UserPoints { Id = "testuser123", Points = 10.0 };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(existingUser);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("Test-User_123!@#,5");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var updatedUser = await assertContext.UserPoints.FindAsync("testuser123");
        Assert.That(updatedUser.Points, Is.EqualTo(5.0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplateWithCorrectData_WhenPointsRemoved()
    {
        var existingUser = new UserPoints { Id = "user1", Points = 10.0 };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(existingUser);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user1,5");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/PointsUpdate",
            Arg.Is<PointsUpdateViewModel>(vm =>
                vm.Username == "user1" &&
                vm.PointsAdded == 5.0 &&
                vm.NewTotal == 5.0 &&
                vm.IsAddition == false
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHtmlBox_WhenSuccessful()
    {
        var existingUser = new UserPoints { Id = "user1", Points = 10.0 };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(existingUser);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user1,5");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<b>Points removed</b>");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtml("<b>Points removed</b>");
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleWhitespaceInTarget_WhenParsing()
    {
        var existingUser = new UserPoints { Id = "user1", Points = 10.0 };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(existingUser);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user1 , 5");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var updatedUser = await assertContext.UserPoints.FindAsync("user1");
        Assert.That(updatedUser.Points, Is.EqualTo(5.0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeLeaderboardInTemplate_WhenPointsRemoved()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 20 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 15 });
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user1,5");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/PointsUpdate",
            Arg.Is<PointsUpdateViewModel>(vm =>
                vm.Leaderboard.Count == 2 &&
                vm.Leaderboard["user1"] == 15 &&
                vm.Leaderboard["user2"] == 15
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldExcludeZeroPointsFromLeaderboard_WhenUserReachesZero()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 10 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 5 });
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user2,10");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/PointsUpdate",
            Arg.Is<PointsUpdateViewModel>(vm =>
                vm.Leaderboard.Count == 1 &&
                vm.Leaderboard.ContainsKey("user1") &&
                !vm.Leaderboard.ContainsKey("user2")
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleError_WhenDatabaseFails()
    {
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BotDbContext>(new Exception("Database error")));

        _context.Target.Returns("user1,10");

        var action = async () => await _command.RunAsync(_context);

        Assert.That(action, Throws.TypeOf<Exception>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCancellationToken_WhenProvided()
    {
        var existingUser = new UserPoints { Id = "user1", Points = 10.0 };

        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(existingUser);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user1,5");
        var cancellationToken = CancellationToken.None;

        await _command.RunAsync(_context, cancellationToken);

        await _dbContextFactory.Received(1).CreateDbContextAsync(cancellationToken);
    }
}
