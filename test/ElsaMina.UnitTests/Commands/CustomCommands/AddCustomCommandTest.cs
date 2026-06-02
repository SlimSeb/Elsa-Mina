using ElsaMina.Commands.CustomCommands;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.CustomCommands;

public class AddCustomCommandTests
{
    private static readonly string[] EXPECTED_ALIASES = ["add-custom", "add-command", "addcommand"];

    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private IConfiguration _configuration;
    private IClockService _clockService;
    private IContext _context;
    private AddCustomCommand _command;

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

        // Setup configuration mock
        _configuration = Substitute.For<IConfiguration>();
        _configuration.Trigger.Returns(".");

        // Setup clock service mock
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTime.Returns(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));

        // Setup context mock
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("testroom");
        _context.Sender.Name.Returns("testuser");

        // Create command instance
        _command = new AddCustomCommand(_configuration, _clockService, _dbContextFactory);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        // Arrange & Act
        var command = new AddCustomCommand(_configuration, _clockService, _dbContextFactory);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("add-custom-command"));
            Assert.That(command.Aliases, Is.EquivalentTo(EXPECTED_ALIASES));
            Assert.That(command.RequiredRank, Is.EqualTo(Rank.Driver));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenTargetHasNoComma()
    {
        // Arrange
        _context.Target.Returns("commandonly");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNameTooLong_WhenCommandNameExceeds18Characters()
    {
        // Arrange
        _context.Target.Returns("thiscommandnameiswaytoolong,content");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("addcommand_name_too_long");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyContentTooLong_WhenContentExceeds300Characters()
    {
        // Arrange
        var longContent = new string('a', 301);
        _context.Target.Returns($"test,{longContent}");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("addcommand_content_too_long");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyBadFirstChar_WhenContentStartsWithTrigger()
    {
        // Arrange
        _configuration.Trigger.Returns(".");
        _context.Target.Returns("test,.another command");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("addcommand_bad_first_char");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyBadFirstChar_WhenContentStartsWithSlash()
    {
        // Arrange
        _context.Target.Returns("test,/command");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("addcommand_bad_first_char");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyBadFirstChar_WhenContentStartsWithExclamation()
    {
        // Arrange
        _context.Target.Returns("test,!command");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("addcommand_bad_first_char");
        await _dbContextFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyExists_WhenCommandAlreadyExistsInRoom()
    {
        // Arrange
        var existingCommand = new AddedCommand
        {
            Id = "existing",
            RoomId = "testroom",
            Content = "old content",
            Author = "olduser",
            CreationDate = DateTime.UtcNow
        };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.AddedCommands.AddAsync(existingCommand);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("existing,new content");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("addcommand_already_exist");
        
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var command = await assertContext.AddedCommands.FindAsync("existing", "testroom");
            Assert.That(command.Content, Is.EqualTo("old content")); // Should not be updated
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddCommand_WhenValidInputProvided()
    {
        // Arrange
        _context.Target.Returns("hello,Hello world!");
        _context.Sender.Name.Returns("alice");
        _context.RoomId.Returns("lobby");

        var expectedDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        _clockService.CurrentUtcDateTime.Returns(expectedDate);

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedCommand = await assertContext.AddedCommands.FindAsync("hello", "lobby");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(addedCommand, Is.Not.Null);
                Assert.That(addedCommand.Id, Is.EqualTo("hello"));
                Assert.That(addedCommand.RoomId, Is.EqualTo("lobby"));
                Assert.That(addedCommand.Content, Is.EqualTo("Hello world!"));
                Assert.That(addedCommand.Author, Is.EqualTo("alice"));
                Assert.That(addedCommand.CreationDate, Is.EqualTo(expectedDate));
            }
        }

        _context.Received(1).ReplyLocalizedMessage("addcommand_success", "hello");
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimCommandName_WhenProcessing()
    {
        // Arrange
        _context.Target.Returns("  hello  ,content");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedCommand = await assertContext.AddedCommands.FindAsync("hello", "testroom");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(addedCommand, Is.Not.Null);
                Assert.That(addedCommand.Id, Is.EqualTo("hello"));
            }
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimContent_WhenProcessing()
    {
        // Arrange
        _context.Target.Returns("test,  content with spaces  ");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedCommand = await assertContext.AddedCommands.FindAsync("test", "testroom");
            Assert.That(addedCommand.Content, Is.EqualTo("content with spaces"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldConvertCommandNameToLowercase_WhenProcessing()
    {
        // Arrange
        _context.Target.Returns("HELLO,content");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedCommand = await assertContext.AddedCommands.FindAsync("hello", "testroom");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(addedCommand, Is.Not.Null);
                Assert.That(addedCommand.Id, Is.EqualTo("hello"));
            }
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleContentWithMultipleCommas_WhenProcessing()
    {
        // Arrange
        _context.Target.Returns("test,content,with,multiple,commas");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedCommand = await assertContext.AddedCommands.FindAsync("test", "testroom");
            Assert.That(addedCommand.Content, Is.EqualTo("content,with,multiple,commas"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldAllowSameCommandInDifferentRooms_WhenAdding()
    {
        // Arrange
        var commandInRoom1 = new AddedCommand
        {
            Id = "test",
            RoomId = "room1",
            Content = "content1",
            Author = "user1",
            CreationDate = DateTime.UtcNow
        };

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.AddedCommands.AddAsync(commandInRoom1);
            await setupContext.SaveChangesAsync();
        }

        _context.Target.Returns("test,content2");
        _context.RoomId.Returns("room2");
        _context.Sender.Name.Returns("user2");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var commandRoom1 = await assertContext.AddedCommands.FindAsync("test", "room1");
            var commandRoom2 = await assertContext.AddedCommands.FindAsync("test", "room2");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(commandRoom1, Is.Not.Null);
                Assert.That(commandRoom2, Is.Not.Null);
                Assert.That(commandRoom1.Content, Is.EqualTo("content1"));
                Assert.That(commandRoom2.Content, Is.EqualTo("content2"));
            }
        }

        _context.Received(1).ReplyLocalizedMessage("addcommand_success", "test");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAccept18CharacterName_WhenAtMaxLength()
    {
        // Arrange
        var maxLengthName = new string('a', 18);
        _context.Target.Returns($"{maxLengthName},content");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedCommand = await assertContext.AddedCommands.FindAsync(maxLengthName, "testroom");
            Assert.That(addedCommand, Is.Not.Null);
        }

        _context.DidNotReceive().ReplyLocalizedMessage("addcommand_name_too_long");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAccept300CharacterContent_WhenAtMaxLength()
    {
        // Arrange
        var maxLengthContent = new string('a', 300);
        _context.Target.Returns($"test,{maxLengthContent}");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedCommand = await assertContext.AddedCommands.FindAsync("test", "testroom");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(addedCommand, Is.Not.Null);
                Assert.That(addedCommand.Content.Length, Is.EqualTo(300));
            }
        }

        _context.DidNotReceive().ReplyLocalizedMessage("addcommand_content_too_long");
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCancellationToken_WhenProvided()
    {
        // Arrange
        _context.Target.Returns("test,content");
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
        _context.Target.Returns("test,content");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _dbContextFactory.Received(1).CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseClockServiceForCreationDate_WhenAdding()
    {
        // Arrange
        var specificDate = new DateTime(2023, 6, 15, 14, 30, 45, DateTimeKind.Utc);
        _clockService.CurrentUtcDateTime.Returns(specificDate);
        _context.Target.Returns("test,content");

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var addedCommand = await assertContext.AddedCommands.FindAsync("test", "testroom");
            Assert.That(addedCommand.CreationDate, Is.EqualTo(specificDate));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotCreateCommand_WhenNameIsExactly19Characters()
    {
        // Arrange
        var tooLongName = new string('a', 19);
        _context.Target.Returns($"{tooLongName},content");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("addcommand_name_too_long");
        
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var commands = await assertContext.AddedCommands.ToListAsync();
            Assert.That(commands, Is.Empty);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotCreateCommand_WhenContentIsExactly301Characters()
    {
        // Arrange
        var tooLongContent = new string('a', 301);
        _context.Target.Returns($"test,{tooLongContent}");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("addcommand_content_too_long");
        
        using (var assertContext = new BotDbContext(_dbOptions))
        {
            var commands = await assertContext.AddedCommands.ToListAsync();
            Assert.That(commands, Is.Empty);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldRespectConfigurationTrigger_WhenCheckingFirstCharacter()
    {
        // Arrange
        _configuration.Trigger.Returns("!");
        _context.Target.Returns("test,!command");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("addcommand_bad_first_char");
    }
}