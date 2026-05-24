using ElsaMina.Commands.CustomCommands;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.AddedCommands;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.CustomCommands;

public class RandomCustomCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private IRandomService _randomService;
    private IAddedCommandsManager _addedCommandsManager;
    private IContext _context;
    private RandomCustomCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new BotDbContext(_dbOptions);
        dbContext.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _randomService = Substitute.For<IRandomService>();
        _addedCommandsManager = Substitute.For<IAddedCommandsManager>();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("testroom");

        _command = new RandomCustomCommand(_dbContextFactory, _randomService, _addedCommandsManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallExecuteAddedCommand_WithRandomCommand_WhenCommandsExist()
    {
        var addedCommand = new AddedCommand
        {
            Id = "hello",
            RoomId = "testroom",
            Content = "Hello world!",
            Author = "alice",
            CreationDate = DateTime.UtcNow
        };

        await using (var seedDb = new BotDbContext(_dbOptions))
        {
            seedDb.AddedCommands.Add(addedCommand);
            await seedDb.SaveChangesAsync();
        }

        _randomService.RandomElement(Arg.Any<IEnumerable<AddedCommand>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<AddedCommand>>().First());

        await _command.RunAsync(_context);

        await _addedCommandsManager.Received(1).ExecuteAddedCommand(
            Arg.Is<AddedCommand>(c => c.Id == "hello" && c.Content == "Hello world!"),
            _context,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallRandomElement_WithRoomCommandsOnly_WhenCommandsExist()
    {
        await using (var seedDb = new BotDbContext(_dbOptions))
        {
            seedDb.AddedCommands.AddRange(
                new AddedCommand { Id = "cmd1", RoomId = "testroom", Content = "Content 1", Author = "a", CreationDate = DateTime.UtcNow },
                new AddedCommand { Id = "cmd2", RoomId = "testroom", Content = "Content 2", Author = "b", CreationDate = DateTime.UtcNow },
                new AddedCommand { Id = "cmd3", RoomId = "otherroom", Content = "Content 3", Author = "c", CreationDate = DateTime.UtcNow }
            );
            await seedDb.SaveChangesAsync();
        }

        IEnumerable<AddedCommand> capturedList = null;
        _randomService.RandomElement(Arg.Any<IEnumerable<AddedCommand>>())
            .Returns(callInfo =>
            {
                capturedList = callInfo.Arg<IEnumerable<AddedCommand>>().ToList();
                return ((List<AddedCommand>)capturedList)[0];
            });

        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedList, Is.Not.Null);
            Assert.That(capturedList.Count(), Is.EqualTo(2));
            Assert.That(capturedList.All(c => c.RoomId == "testroom"), Is.True);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenDbContextFactoryThrows()
    {
        var exception = new Exception("DB connection failed");
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(exception);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("randcustom_error", "DB connection failed");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenRandomElementThrows()
    {
        await using (var seedDb = new BotDbContext(_dbOptions))
        {
            seedDb.AddedCommands.Add(new AddedCommand
            {
                Id = "cmd1",
                RoomId = "testroom",
                Content = "Content",
                Author = "a",
                CreationDate = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        var exception = new InvalidOperationException("Sequence contains no elements");
        _randomService.RandomElement(Arg.Any<IEnumerable<AddedCommand>>())
            .Throws(exception);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("randcustom_error", "Sequence contains no elements");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotCallReply_WhenCommandsExistAndNoError()
    {
        await using (var seedDb = new BotDbContext(_dbOptions))
        {
            seedDb.AddedCommands.Add(new AddedCommand
            {
                Id = "cmd1",
                RoomId = "testroom",
                Content = "Content",
                Author = "a",
                CreationDate = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        _randomService.RandomElement(Arg.Any<IEnumerable<AddedCommand>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<AddedCommand>>().First());

        await _command.RunAsync(_context);

        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCancellationToken_WhenProvided()
    {
        _randomService.RandomElement(Arg.Any<IEnumerable<AddedCommand>>())
            .Returns(new AddedCommand { Id = "x", RoomId = "r", Content = "c", Author = "a", CreationDate = DateTime.UtcNow });

        var token = CancellationToken.None;

        await _command.RunAsync(_context, token);

        await _dbContextFactory.Received(1).CreateDbContextAsync(token);
    }
}
