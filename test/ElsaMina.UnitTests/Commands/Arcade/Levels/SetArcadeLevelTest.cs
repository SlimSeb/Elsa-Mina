using ElsaMina.Commands.Arcade.Levels;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Arcade.Levels;

public class SetArcadeLevelCommandTests
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private IContext _context;
    private SetArcadeLevelCommand _command;

    [SetUp]
    public void SetUp()
    {
        // Create in-memory database options
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
        _command = new SetArcadeLevelCommand(_dbContextFactory);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        // Arrange & Act
        var command = new SetArcadeLevelCommand(_dbContextFactory);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("addpalier"));
            Assert.That(command.Aliases, Is.EquivalentTo(new[] { "setpalier" }));
            Assert.That(command.RequiredRank, Is.EqualTo(Rank.Driver));
            Assert.That(command.RoomRestriction, Is.EqualTo(new[] { "arcade", "botdevelopment" }));
            Assert.That(command.HelpMessageKey, Is.EqualTo("arcade_level_help"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenTargetIsNull()
    {
        // Arrange
        _context.Target.Returns((string)null);
        _context.GetString("arcade_level_help").Returns("Help message");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("arcade_level_help");
        _context.Received(1).Reply("Help message");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenTargetHasNoComma()
    {
        // Arrange
        _context.Target.Returns("user1");
        _context.GetString("arcade_level_help").Returns("Help message");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("arcade_level_help");
        _context.Received(1).Reply("Help message");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenLevelIsNotANumber()
    {
        // Arrange
        _context.Target.Returns("user1,notanumber");
        _context.GetString("arcade_level_help").Returns("Help message");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString("arcade_level_help");
        _context.Received(1).Reply("Help message");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidValue_WhenLevelIsZero()
    {
        // Arrange
        _context.Target.Returns("user1,0");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("arcade_level_invalid_value");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidValue_WhenLevelIsNegative()
    {
        // Arrange
        _context.Target.Returns("user1,-5");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("arcade_level_invalid_value");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidValue_WhenLevelIsGreaterThan50()
    {
        // Arrange
        _context.Target.Returns("user1,51");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("arcade_level_invalid_value");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddNewLevel_WhenUserDoesNotExist()
    {
        // Arrange
        _context.Target.Returns("newuser,10");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedLevel = await assertContext.ArcadeLevels.FindAsync("newuser");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(addedLevel, Is.Not.Null);
                Assert.That(addedLevel.Id, Is.EqualTo("newuser"));
                Assert.That(addedLevel.Level, Is.EqualTo(10));
            }
        }

        _context.Received(1).ReplyLocalizedMessage("arcade_level_add", "newuser", 10);
        _context.DidNotReceive().ReplyLocalizedMessage("arcade_level_update", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUpdateExistingLevel_WhenUserExists()
    {
        // Arrange
        var existingLevel = new ArcadeLevel { Id = "existinguser", Level = 5 };
        
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(existingLevel);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("existinguser,20");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var updatedLevel = await assertContext.ArcadeLevels.FindAsync("existinguser");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(updatedLevel, Is.Not.Null);
                Assert.That(updatedLevel.Level, Is.EqualTo(20));
            }
        }

        _context.Received(1).ReplyLocalizedMessage("arcade_level_update", "existinguser", 20);
        _context.DidNotReceive().ReplyLocalizedMessage("arcade_level_add", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeUserId_WhenTargetContainsSpecialCharacters()
    {
        // Arrange
        _context.Target.Returns("Test-User_123!@#,15");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedLevel = await assertContext.ArcadeLevels.FindAsync("testuser123");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(addedLevel, Is.Not.Null);
                Assert.That(addedLevel.Level, Is.EqualTo(15));
            }
        }

        _context.Received(1).ReplyLocalizedMessage("arcade_level_add", "testuser123", 15);
    }

    [Test]
    public async Task Test_RunAsync_ShouldAcceptLevel1_WhenLevelIsMinimumValid()
    {
        // Arrange
        _context.Target.Returns("user1,1");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedLevel = await assertContext.ArcadeLevels.FindAsync("user1");
            Assert.That(addedLevel.Level, Is.EqualTo(1));
        }

        _context.Received(1).ReplyLocalizedMessage("arcade_level_add", "user1", 1);
        _context.DidNotReceive().ReplyLocalizedMessage("arcade_level_invalid_value");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAcceptLevel50_WhenLevelIsMaximumValid()
    {
        // Arrange
        _context.Target.Returns("user1,50");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedLevel = await assertContext.ArcadeLevels.FindAsync("user1");
            Assert.That(addedLevel.Level, Is.EqualTo(50));
        }

        _context.Received(1).ReplyLocalizedMessage("arcade_level_add", "user1", 50);
        _context.DidNotReceive().ReplyLocalizedMessage("arcade_level_invalid_value");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenAddingNewLevelFails()
    {
        // Arrange
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Throws(new Exception("Database error"));

        _context.Target.Returns("newuser,10");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("arcade_level_update_error", "Database error");
        _context.DidNotReceive().ReplyLocalizedMessage("arcade_level_add", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenUpdatingExistingLevelFails()
    {
        // Arrange
        var existingLevel = new ArcadeLevel { Id = "existinguser", Level = 5 };
        
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(existingLevel);
            await setupContext.SaveChangesAsync();
        }

        var mockContext = Substitute.ForPartsOf<BotDbContext>(_dbOptions);
        mockContext.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Throws(new DbUpdateException("Database error"));

        // Setup the entity to be found
        var levelToFind = new ArcadeLevel { Id = "existinguser", Level = 5 };
        mockContext.FindAsync<ArcadeLevel>(Arg.Any<object[]>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(levelToFind));

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(mockContext);

        _context.Target.Returns("existinguser,20");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("arcade_level_update_error", "Database error");
        _context.DidNotReceive().ReplyLocalizedMessage("arcade_level_update", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCancellationToken_WhenProvided()
    {
        // Arrange
        _context.Target.Returns("user1,10");
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
        _context.Target.Returns("user1,10");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _dbContextFactory.Received(1).CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleWhitespaceInTarget_WhenParsing()
    {
        // Arrange
        _context.Target.Returns("user1 , 10");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedLevel = await assertContext.ArcadeLevels.FindAsync("user1");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(addedLevel, Is.Not.Null);
                Assert.That(addedLevel.Level, Is.EqualTo(10));
            }
        }

        _context.Received(1).ReplyLocalizedMessage("arcade_level_add", "user1", 10);
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotModifyOtherUsers_WhenUpdatingSpecificUser()
    {
        // Arrange
        var user1 = new ArcadeLevel { Id = "user1", Level = 5 };
        var user2 = new ArcadeLevel { Id = "user2", Level = 10 };
        var user3 = new ArcadeLevel { Id = "user3", Level = 15 };
        
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddRangeAsync(user1, user2, user3);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("user2,25");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var updatedUser1 = await assertContext.ArcadeLevels.FindAsync("user1");
            var updatedUser2 = await assertContext.ArcadeLevels.FindAsync("user2");
            var updatedUser3 = await assertContext.ArcadeLevels.FindAsync("user3");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(updatedUser1.Level, Is.EqualTo(5));
                Assert.That(updatedUser2.Level, Is.EqualTo(25));
                Assert.That(updatedUser3.Level, Is.EqualTo(15));
            }
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleCaseInsensitiveUserId_WhenProcessing()
    {
        // Arrange
        var existingLevel = new ArcadeLevel { Id = "testuser", Level = 5 };
        
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(existingLevel);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("TESTUSER,20");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var updatedLevel = await assertContext.ArcadeLevels.FindAsync("testuser");
            Assert.That(updatedLevel.Level, Is.EqualTo(20));
        }

        _context.Received(1).ReplyLocalizedMessage("arcade_level_update", "testuser", 20);
    }
}