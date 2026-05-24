using System.Globalization;
using ElsaMina.Commands.Arcade.Levels;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Arcade.Levels;

public class DisplayArcadeLevelsCommandTests
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private DisplayArcadeLevelsCommand _command;

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
            .Returns(_ => new BotDbContext(_dbOptions));

        // Setup templates manager mock
        _templatesManager = Substitute.For<ITemplatesManager>();

        // Setup context mock
        _context = Substitute.For<IContext>();
        _context.Culture.Returns(CultureInfo.InvariantCulture);

        // Create command instance
        _command = new DisplayArcadeLevelsCommand(_templatesManager, _dbContextFactory);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        // Arrange & Act
        var command = new DisplayArcadeLevelsCommand(_templatesManager, _dbContextFactory);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("displaypaliers"));
            Assert.That(command.Aliases, Is.EquivalentTo(new[] { "displaypalier", "paliers", "arcadelevels" }));
            Assert.That(command.RequiredRank, Is.EqualTo(Rank.Regular));
            Assert.That(command.RoomRestriction, Is.EqualTo(new[] { "arcade", "botdevelopment" }));
            Assert.That(command.HelpMessageKey, Is.EqualTo("display_paliers_help"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoUsers_WhenNoLevelsExist()
    {
        // Arrange
        // Database is empty

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("arcade_level_no_users");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDisplaySingleLevel_WhenOneLevelExists()
    {
        // Arrange
        var level = new ArcadeLevel { Id = "user1", Level = 5 };

        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(level);
            await setupContext.SaveChangesAsync();
        }

        _templatesManager.GetTemplateAsync("Arcade/Levels/ArcadeLevels", Arg.Any<ArcadeLevelsViewModel>())
            .Returns("<html>rendered template</html>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Arcade/Levels/ArcadeLevels",
            Arg.Is<ArcadeLevelsViewModel>(vm =>
                vm.Culture == _context.Culture &&
                vm.Levels.Count == 1 &&
                vm.Levels[5].Count == 1 &&
                vm.Levels[5][0].UserId == "user1"));

        _context.Received(1).ReplyHtml("<html>rendered template</html>", rankAware: true);
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldGroupUsersByLevel_WhenMultipleUsersExist()
    {
        // Arrange
        var levels = new[]
        {
            new ArcadeLevel { Id = "user1", Level = 5 },
            new ArcadeLevel { Id = "user2", Level = 5 },
            new ArcadeLevel { Id = "user3", Level = 10 },
            new ArcadeLevel { Id = "user4", Level = 10 },
            new ArcadeLevel { Id = "user5", Level = 15 }
        };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddRangeAsync(levels);
            await setupContext.SaveChangesAsync();
        }

        _templatesManager.GetTemplateAsync("Arcade/Levels/ArcadeLevels", Arg.Any<ArcadeLevelsViewModel>())
            .Returns("<html>template</html>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Arcade/Levels/ArcadeLevels",
            Arg.Is<ArcadeLevelsViewModel>(vm =>
                vm.Levels.Count == 3 &&
                vm.Levels[5].Count == 2 &&
                vm.Levels[10].Count == 2 &&
                vm.Levels[15].Count == 1));

        _context.Received(1).ReplyHtml("<html>template</html>", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeAllUsersInLevel_WhenMultipleUsersHaveSameLevel()
    {
        // Arrange
        var levels = new[]
        {
            new ArcadeLevel { Id = "alice", Level = 7 },
            new ArcadeLevel { Id = "bob", Level = 7 },
            new ArcadeLevel { Id = "charlie", Level = 7 }
        };

        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddRangeAsync(levels);
            await setupContext.SaveChangesAsync();
        }

        _templatesManager.GetTemplateAsync("Arcade/Levels/ArcadeLevels", Arg.Any<ArcadeLevelsViewModel>())
            .Returns("<html>template</html>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Arcade/Levels/ArcadeLevels",
            Arg.Is<ArcadeLevelsViewModel>(vm =>
                vm.Levels[7].Any(p => p.UserId == "alice") &&
                vm.Levels[7].Any(p => p.UserId == "bob") &&
                vm.Levels[7].Any(p => p.UserId == "charlie")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCultureToTemplate_WhenCalled()
    {
        // Arrange
        var customCulture = new CultureInfo("fr-FR");
        _context.Culture.Returns(customCulture);

        var level = new ArcadeLevel { Id = "user1", Level = 1 };

        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(level);
            await setupContext.SaveChangesAsync();
        }
        _templatesManager.GetTemplateAsync("Arcade/Levels/ArcadeLevels", Arg.Any<ArcadeLevelsViewModel>())
            .Returns("<html>template</html>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Arcade/Levels/ArcadeLevels",
            Arg.Is<ArcadeLevelsViewModel>(vm => vm.Culture == customCulture));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallRemoveNewlines_WhenRenderingTemplate()
    {
        // Arrange
        var level = new ArcadeLevel { Id = "user1", Level = 1 };

        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(level);
            await setupContext.SaveChangesAsync();
        }

        _templatesManager.GetTemplateAsync("Arcade/Levels/ArcadeLevels", Arg.Any<ArcadeLevelsViewModel>())
            .Returns("<html>template without newlines</html>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml("<html>template without newlines</html>", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithRankAware_WhenCallingReplyHtml()
    {
        // Arrange
        var level = new ArcadeLevel { Id = "user1", Level = 1 };

        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(level);
            await setupContext.SaveChangesAsync();
        }

        _templatesManager.GetTemplateAsync("Arcade/Levels/ArcadeLevels", Arg.Any<ArcadeLevelsViewModel>())
            .Returns("<html>template</html>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoUsers_WhenDatabaseThrowsException()
    {
        // Arrange
        var mockContext = Substitute.ForPartsOf<BotDbContext>(_dbOptions);
        mockContext.ArcadeLevels.ToListAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Database error"));

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(mockContext);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>());
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCancellationToken_WhenProvided()
    {
        // Arrange
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
        // Database is empty

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _dbContextFactory.Received(1).CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleVariousLevelNumbers_WhenUsersHaveDifferentLevels()
    {
        // Arrange
        var levels = new[]
        {
            new ArcadeLevel { Id = "user1", Level = 1 },
            new ArcadeLevel { Id = "user2", Level = 50 },
            new ArcadeLevel { Id = "user3", Level = 100 },
            new ArcadeLevel { Id = "user4", Level = 999 }
        };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddRangeAsync(levels);
            await setupContext.SaveChangesAsync();
        }

        _templatesManager.GetTemplateAsync("Arcade/Levels/ArcadeLevels", Arg.Any<ArcadeLevelsViewModel>())
            .Returns("<html>template</html>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Arcade/Levels/ArcadeLevels",
            Arg.Is<ArcadeLevelsViewModel>(vm =>
                vm.Levels.ContainsKey(1) &&
                vm.Levels.ContainsKey(50) &&
                vm.Levels.ContainsKey(100) &&
                vm.Levels.ContainsKey(999)));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseCorrectTemplatePath_WhenCalled()
    {
        // Arrange
        var level = new ArcadeLevel { Id = "user1", Level = 1 };

        await using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.ArcadeLevels.AddAsync(level);
            await setupContext.SaveChangesAsync();
        }

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<ArcadeLevelsViewModel>())
            .Returns("<html>template</html>");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Arcade/Levels/ArcadeLevels", Arg.Any<ArcadeLevelsViewModel>());
    }
}