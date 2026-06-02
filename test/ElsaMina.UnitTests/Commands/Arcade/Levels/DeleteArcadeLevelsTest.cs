using ElsaMina.Commands.Arcade.Levels;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Arcade.Levels;

public class DeleteArcadeLevelCommandTests
{
    private static readonly string[] EXPECTED_ALIASES = ["removepalier", "removelevel"];
    private static readonly string[] EXPECTED_ROOM_RESTRICTION = ["arcade", "botdevelopment"];

    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private IContext _context;
    private DeleteArcadeLevelCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        // Clear the database before each test
        using var dbContext = new BotDbContext(_dbOptions);
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        // Setup factory mock to return new contexts
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => new BotDbContext(_dbOptions));

        // Setup context mock
        _context = Substitute.For<IContext>();

        // Create command instance
        _command = new DeleteArcadeLevelCommand(_dbContextFactory);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        // Arrange & Act
        var command = new DeleteArcadeLevelCommand(_dbContextFactory);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("deletepalier"));
            Assert.That(command.Aliases, Is.EquivalentTo(EXPECTED_ALIASES));
            Assert.That(command.RequiredRank, Is.EqualTo(Rank.Driver));
            Assert.That(command.RoomRestriction, Is.EqualTo(EXPECTED_ROOM_RESTRICTION));
            Assert.That(command.HelpMessageKey, Is.EqualTo("arcade_level_delete_help"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenTargetIsNull()
    {
        // Arrange
        _context.Target.Returns((string)null);
        _context.GetString("arcade_level_delete_help").Returns("Help message");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("arcade_level_delete_help");
        _context.Received(1).Reply("Help message");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);
        _context.GetString("arcade_level_delete_help").Returns("Help message");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("arcade_level_delete_help");
        _context.Received(1).Reply("Help message");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenTargetIsWhitespace()
    {
        // Arrange
        _context.Target.Returns("   ");
        _context.GetString("arcade_level_delete_help").Returns("Help message");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("arcade_level_delete_help");
        _context.Received(1).Reply("Help message");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenLevelDoesNotExist()
    {
        // Arrange
        _context.Target.Returns("testuser");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("arcade_level_delete_not_found");
        _context.DidNotReceive().ReplyLocalizedMessage("arcade_level_delete_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeleteLevelAndReplySuccess_WhenLevelExists()
    {
        // Arrange
        var userId = "testuser";
        var arcadeLevel = new ArcadeLevel { Id = userId, Level = 5 };
        
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(arcadeLevel);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("Test-User!");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var deletedLevel = await assertContext.ArcadeLevels.FindAsync(userId);
            Assert.That(deletedLevel, Is.Null);
        }
        
        _context.Received(1).ReplyLocalizedMessage("arcade_level_delete_success");
        _context.DidNotReceive().ReplyLocalizedMessage("arcade_level_delete_not_found");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeId_WhenTargetContainsSpecialCharacters()
    {
        // Arrange
        var userId = "testuser123";
        var arcadeLevel = new ArcadeLevel { Id = userId, Level = 10 };
        
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(arcadeLevel);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("Test-User_123!@#");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using (var assertContext = new BotDbContext(_dbOptions))
        {
            var deletedLevel = await assertContext.ArcadeLevels.FindAsync(userId);
            Assert.That(deletedLevel, Is.Null);
        }
        
        _context.Received(1).ReplyLocalizedMessage("arcade_level_delete_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFailure_WhenSaveChangesFails()
    {
        // Arrange
        var mockContext = Substitute.ForPartsOf<BotDbContext>(_dbOptions);
        
        var userId = "testuser";
        var arcadeLevel = new ArcadeLevel { Id = userId, Level = 3 };

        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(arcadeLevel);
            await setupContext.SaveChangesAsync();
        }

        mockContext.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Throws(new DbUpdateException("Database error"));

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(mockContext);

        _context.Target.Returns("testuser");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("arcade_level_delete_failure", "Database error");
        _context.DidNotReceive().ReplyLocalizedMessage("arcade_level_delete_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCancellationToken_WhenProvided()
    {
        // Arrange
        _context.Target.Returns("testuser");
        var cancellationToken = new CancellationToken();

        // Act
        await _command.RunAsync(_context, cancellationToken);

        // Assert
        await _dbContextFactory.Received(1).CreateDbContextAsync(cancellationToken);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateNewDbContext_WhenCalled()
    {
        // Arrange
        _context.Target.Returns("testuser");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _dbContextFactory.Received(1).CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleCaseInsensitiveId_WhenSearchingForLevel()
    {
        // Arrange
        var userId = "testuser";
        var arcadeLevel = new ArcadeLevel { Id = userId, Level = 7 };
        
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(arcadeLevel);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("TESTUSER");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using (var assertContext = new BotDbContext(_dbOptions))
        {
            var deletedLevel = await assertContext.ArcadeLevels.FindAsync(userId);
            Assert.That(deletedLevel, Is.Null);
        }
        
        _context.Received(1).ReplyLocalizedMessage("arcade_level_delete_success");
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeleteCorrectUser_WhenMultipleUsersExist()
    {
        // Arrange
        var user1 = new ArcadeLevel { Id = "user1", Level = 5 };
        var user2 = new ArcadeLevel { Id = "user2", Level = 10 };
        var user3 = new ArcadeLevel { Id = "user3", Level = 15 };

        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddRangeAsync(user1, user2, user3);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user2");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using (var assertContext = new BotDbContext(_dbOptions))
        {
            var deletedUser = await assertContext.ArcadeLevels.FindAsync("user2");
            var stillExistsUser1 = await assertContext.ArcadeLevels.FindAsync("user1");
            var stillExistsUser3 = await assertContext.ArcadeLevels.FindAsync("user3");
            
            using (Assert.EnterMultipleScope())
            {
                Assert.That(deletedUser, Is.Null);
                Assert.That(stillExistsUser1, Is.Not.Null);
                Assert.That(stillExistsUser3, Is.Not.Null);
            }
        }
        
        _context.Received(1).ReplyLocalizedMessage("arcade_level_delete_success");
    }
}