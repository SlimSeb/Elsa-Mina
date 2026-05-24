using ElsaMina.Commands.Arcade.Points;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Points;

public class AddPointsCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private AddPointsCommand _command;

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

        _command = new AddPointsCommand(_dbContextFactory, _templatesManager);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        var command = new AddPointsCommand(_dbContextFactory, _templatesManager);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("addp"));
            Assert.That(command.RequiredRank, Is.EqualTo(Rank.Voiced));
            Assert.That(command.HelpMessageKey, Is.EqualTo("add_points_help"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenTargetHasNoComma()
    {
        _context.Target.Returns("user1");
        _context.GetString("add_points_help").Returns("Help message");

        await _command.RunAsync(_context);

        _context.Received(1).GetString("add_points_help");
        _context.Received(1).Reply("Help message");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenPointsIsNotANumber()
    {
        _context.Target.Returns("user1,notanumber");
        _context.GetString("add_points_help").Returns("Help message");

        await _command.RunAsync(_context);

        _context.Received(1).GetString("add_points_help");
        _context.Received(1).Reply("Help message");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddNewUser_WhenUserDoesNotExist()
    {
        _context.Target.Returns("newuser,10.5");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var addedUser = await assertContext.UserPoints.FindAsync("newuser");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(addedUser, Is.Not.Null);
            Assert.That(addedUser.Id, Is.EqualTo("newuser"));
            Assert.That(addedUser.Points, Is.EqualTo(10.5));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddPointsToExistingUser_WhenUserExists()
    {
        var existingUser = new UserPoints { Id = "existinguser", Points = 5.0 };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(existingUser);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("existinguser,3.5");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var updatedUser = await assertContext.UserPoints.FindAsync("existinguser");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser.Points, Is.EqualTo(8.5));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleIntegerPoints_WhenNoDecimal()
    {
        _context.Target.Returns("user1,10");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var addedUser = await assertContext.UserPoints.FindAsync("user1");
        Assert.That(addedUser.Points, Is.EqualTo(10.0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeUserId_WhenTargetContainsSpecialCharacters()
    {
        _context.Target.Returns("Test-User_123!@#,5");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var addedUser = await assertContext.UserPoints.FindAsync("testuser123");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(addedUser, Is.Not.Null);
            Assert.That(addedUser.Points, Is.EqualTo(5.0));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplateWithCorrectData_WhenPointsAdded()
    {
        _context.Target.Returns("user1,5");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/PointsUpdate",
            Arg.Is<PointsUpdateViewModel>(vm =>
                vm.Username == "user1" &&
                vm.PointsAdded == 5.0 &&
                vm.NewTotal == 5.0 &&
                vm.IsAddition == true
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHtmlBox_WhenSuccessful()
    {
        _context.Target.Returns("user1,10");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<b>Points added</b>");

        await _command.RunAsync(_context);

        _context.Received(1).Reply("/addhtmlbox <b>Points added</b>");
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleNegativePoints_WhenSubtracting()
    {
        var existingUser = new UserPoints { Id = "user1", Points = 10.0 };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(existingUser);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user1,-5");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var updatedUser = await assertContext.UserPoints.FindAsync("user1");
        Assert.That(updatedUser.Points, Is.EqualTo(5.0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleWhitespaceInTarget_WhenParsing()
    {
        _context.Target.Returns("user1 , 10");

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var addedUser = await assertContext.UserPoints.FindAsync("user1");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(addedUser, Is.Not.Null);
            Assert.That(addedUser.Points, Is.EqualTo(10.0));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeLeaderboardInTemplate_WhenPointsAdded()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 15 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 5 });
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user3,20");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/PointsUpdate",
            Arg.Is<PointsUpdateViewModel>(vm =>
                vm.Leaderboard.Count == 3 &&
                vm.Leaderboard["user3"] == 20 &&
                vm.Leaderboard["user1"] == 15 &&
                vm.Leaderboard["user2"] == 5
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldExcludeZeroPointsFromLeaderboard_WhenFetching()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 10 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 0 });
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user3,5");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Points/PointsUpdate",
            Arg.Is<PointsUpdateViewModel>(vm =>
                vm.Leaderboard.Count == 2 &&
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
        _context.Target.Returns("user1,10");
        var cancellationToken = CancellationToken.None;

        await _command.RunAsync(_context, cancellationToken);

        await _dbContextFactory.Received(1).CreateDbContextAsync(cancellationToken);
    }
}
