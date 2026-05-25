using ElsaMina.Commands.Arcade.Points;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Points;

public class ClearPointsCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private IContext _context;
    private ClearPointsCommand _command;

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

        _context = Substitute.For<IContext>();

        _command = new ClearPointsCommand(_dbContextFactory);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        var command = new ClearPointsCommand(_dbContextFactory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("clearpoints"));
            Assert.That(command.RequiredRank, Is.EqualTo(Rank.Voiced));
            Assert.That(command.HelpMessageKey, Is.EqualTo("clear_points_help"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeleteAllPoints_WhenCalled()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 10 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 20 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user3", Points = 15 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var remainingPoints = await assertContext.UserPoints.CountAsync();
        Assert.That(remainingPoints, Is.Zero);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySuccess_WhenPointsCleared()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 10 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("clear_points_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSucceed_WhenNoPointsExist()
    {
        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var remainingPoints = await assertContext.UserPoints.CountAsync();
        Assert.That(remainingPoints, Is.Zero);

        _context.Received(1).ReplyLocalizedMessage("clear_points_success");
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
        var cancellationToken = CancellationToken.None;

        await _command.RunAsync(_context, cancellationToken);

        await _dbContextFactory.Received(1).CreateDbContextAsync(cancellationToken);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeleteOnlyUserPoints_WhenOtherTablesExist()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 10 });
            await setupContext.ArcadeLevels.AddAsync(new ArcadeLevel { Id = "user1", Level = 5 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var remainingPoints = await assertContext.UserPoints.CountAsync();
        var remainingLevels = await assertContext.ArcadeLevels.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(remainingPoints, Is.Zero);
            Assert.That(remainingLevels, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldClearAllPoints_WhenManyUsersExist()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            for (int index = 0; index < 100; index++)
            {
                await setupContext.UserPoints.AddAsync(new UserPoints { Id = $"user{index}", Points = index });
            }
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var remainingPoints = await assertContext.UserPoints.CountAsync();
        Assert.That(remainingPoints, Is.Zero);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleDecimalPoints_WhenClearing()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 10.5 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 7.25 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var remainingPoints = await assertContext.UserPoints.CountAsync();
        Assert.That(remainingPoints, Is.Zero);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleZeroPoints_WhenClearing()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = 0 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 0 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var remainingPoints = await assertContext.UserPoints.CountAsync();
        Assert.That(remainingPoints, Is.Zero);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleNegativePoints_WhenClearing()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user1", Points = -5 });
            await setupContext.UserPoints.AddAsync(new UserPoints { Id = "user2", Points = 10 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        using var assertContext = new BotDbContext(_dbOptions);
        var remainingPoints = await assertContext.UserPoints.CountAsync();
        Assert.That(remainingPoints, Is.Zero);
    }
}
