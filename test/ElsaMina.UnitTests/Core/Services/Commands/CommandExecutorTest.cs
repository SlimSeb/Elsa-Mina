using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.AddedCommands;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Telemetry;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Commands;

public class CommandExecutorTest
{
    private IDependencyContainerService _dependencyContainerService;
    private IAddedCommandsManager _addedCommandsManager;
    private ITelemetryService _telemetryService;
    private CommandExecutor _commandExecutor;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _addedCommandsManager = Substitute.For<IAddedCommandsManager>();
        _telemetryService = Substitute.For<ITelemetryService>();
        _context = Substitute.For<IContext>();
        _commandExecutor = new CommandExecutor(_dependencyContainerService, _addedCommandsManager,
            Enumerable.Empty<IDynamicCommandProvider>(), _telemetryService);
    }

    [Test]
    public void Test_GetAllCommands_ShouldReturnAllCommandsFromContainer()
    {
        // Arrange
        var expectedCommands = new List<ICommand>
        {
            Substitute.For<ICommand>(),
            Substitute.For<ICommand>()
        };
        expectedCommands.ElementAt(0).Name.Returns("1");
        expectedCommands.ElementAt(1).Name.Returns("2");
        _dependencyContainerService.GetAllNamedRegistrations<ICommand>().Returns(expectedCommands);

        // Act
        var result = _commandExecutor.GetAllCommands();

        // Assert
        Assert.That(result, Is.EquivalentTo(expectedCommands));
    }

    [Test]
    public void Test_GetAllCommands_ShouldReturnDistinctCommandsByName()
    {
        // Arrange
        var command1 = Substitute.For<ICommand>();
        command1.Name.Returns("cmd1");
        var command2 = Substitute.For<ICommand>();
        command2.Name.Returns("cmd2");
        var duplicateCommand = Substitute.For<ICommand>();
        duplicateCommand.Name.Returns("cmd1");
        _dependencyContainerService.GetAllNamedRegistrations<ICommand>()
            .Returns([command1, command2, duplicateCommand]);

        // Act
        var result = _commandExecutor.GetAllCommands().ToList();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result, Does.Contain(command1));
            Assert.That(result, Does.Contain(command2));
        }
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldRunCommand_WhenRegisteredAndAllowed()
    {
        // Arrange
        var commandName = "testCommand";
        var command = Substitute.For<ICommand>();
        var runSignal = new TaskCompletionSource<bool>();
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(true);
        _dependencyContainerService.ResolveNamed<ICommand>(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await Task.WhenAny(runSignal.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        // Assert
        await command.Received(1).RunAsync(_context, Arg.Any<CancellationToken>());
    }

    [Test]
    [TestCase(null, 0)]
    [TestCase("", 0)]
    [TestCase("franais", 1)]
    [TestCase("other", 0)]
    public async Task Test_TryExecuteCommandAsync_ShouldRunCommand_WhenIsNotInRoomRestriction(string roomId,
        int expectedRunCalls)
    {
        // Arrange
        var commandName = "testCommand";
        var command = Substitute.For<ICommand>();
        var runSignal = new TaskCompletionSource<bool>();
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(true);
        _dependencyContainerService.ResolveNamed<ICommand>(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.RoomRestriction.Returns(["franais"]);
        _context.RoomId.Returns(roomId);
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        if (expectedRunCalls > 0)
        {
            await Task.WhenAny(runSignal.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        }

        // Assert
        await command.Received(expectedRunCalls).RunAsync(_context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotRunCommand_WhenNotAllowedByPrivateMessageRestriction()
    {
        // Arrange
        var commandName = "testCommand";
        var command = Substitute.For<ICommand>();
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(true);
        _dependencyContainerService.ResolveNamed<ICommand>(commandName).Returns(command);
        _context.IsPrivateMessage.Returns(true);
        command.IsPrivateMessageOnly.Returns(false);
        command.IsAllowedInPrivateMessage.Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Assert
        await command.DidNotReceive().RunAsync(_context);
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotRunCommand_WhenNotWhitelisted()
    {
        // Arrange
        var commandName = "whitelistCommand";
        var command = Substitute.For<ICommand>();
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(true);
        _dependencyContainerService.ResolveNamed<ICommand>(commandName).Returns(command);
        command.IsWhitelistOnly.Returns(true);
        _context.IsSenderWhitelisted.Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Assert
        await command.DidNotReceive().RunAsync(_context);
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotRunCommand_WhenRankIsTooLow()
    {
        // Arrange
        var commandName = "rankedCommand";
        var command = Substitute.For<ICommand>();
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(true);
        _dependencyContainerService.ResolveNamed<ICommand>(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Assert
        await command.DidNotReceive().RunAsync(_context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldRunCommand_WhenAllowedInPrivateMessage()
    {
        // Arrange
        var commandName = "pmCommand";
        var command = Substitute.For<ICommand>();
        var runSignal = new TaskCompletionSource<bool>();
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(true);
        _dependencyContainerService.ResolveNamed<ICommand>(commandName).Returns(command);
        _context.IsPrivateMessage.Returns(true);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await Task.WhenAny(runSignal.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        // Assert
        await command.Received(1).RunAsync(_context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldThrow_WhenCommandExecutionFails()
    {
        // Arrange
        var commandName = "failingCommand";
        var command = Substitute.For<ICommand>();
        var expectedException = new InvalidOperationException("boom");
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(true);
        _dependencyContainerService.ResolveNamed<ICommand>(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.RoomRestriction.Returns(Array.Empty<string>());
        command.Name.Returns(commandName);
        command.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(expectedException));

        // Act
        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _commandExecutor.TryExecuteCommandAsync(commandName, _context));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception, Is.SameAs(expectedException));
            Assert.That(_commandExecutor.RunningCommands, Is.Empty);
        }
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldTryAddedCommand_WhenNotRegisteredAndNotPrivateMessage()
    {
        // Arrange
        var commandName = "customCommand";
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(false);
        _context.IsPrivateMessage.Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Assert
        await _addedCommandsManager.Received(1).TryExecuteAddedCommand(commandName, _context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldLogError_WhenCommandNotFoundAndIsPrivateMessage()
    {
        // Arrange
        var commandName = "missingCommand";
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(false);
        _context.IsPrivateMessage.Returns(true);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Assert
        await _addedCommandsManager.DidNotReceive().TryExecuteAddedCommand(commandName, _context, Arg.Any<CancellationToken>());
    }


    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldExecuteAddedCommand_WhenCommandNotRegisteredAndNotPrivateMessage()
    {
        // Arrange
        var commandName = "customCommand";
        var context = Substitute.For<IContext>();
        context.IsPrivateMessage.Returns(false);
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, context);

        // Assert
        await _addedCommandsManager.Received().TryExecuteAddedCommand(commandName, context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldReplyAutoCorrect_WhenEnabledInRoom()
    {
        // Arrange
        var commandName = "hep";
        var room = Substitute.For<IRoom>();
        var command = Substitute.For<ICommand>();
        command.Name.Returns("help");
        command.Aliases.Returns(Array.Empty<string>());
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(false);
        _dependencyContainerService.GetAllNamedRegistrations<ICommand>().Returns([command]);
        _addedCommandsManager.TryExecuteAddedCommand(commandName, _context, Arg.Any<CancellationToken>()).Returns(false);
        _context.IsPrivateMessage.Returns(false);
        _context.Room.Returns(room);
        room.GetParameterValueAsync(Parameter.HasCommandAutoCorrect, Arg.Any<CancellationToken>())
            .Returns("true");

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Assert
        _context.Received(1)
            .ReplyLocalizedMessage("command_autocorrect_suggestion", commandName, "help");
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldReplyAutoCorrect_WhenPrivateMessage()
    {
        // Arrange
        var commandName = "hep";
        var room = Substitute.For<IRoom>();
        var command = Substitute.For<ICommand>();
        command.Name.Returns("help");
        command.Aliases.Returns(Array.Empty<string>());
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(false);
        _dependencyContainerService.GetAllNamedRegistrations<ICommand>().Returns([command]);
        _context.IsPrivateMessage.Returns(true);
        _context.Room.Returns(room);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Assert
        _context.Received(1)
            .ReplyLocalizedMessage("command_autocorrect_suggestion", commandName, "help");
        await _addedCommandsManager.DidNotReceive().TryExecuteAddedCommand(commandName, _context, Arg.Any<CancellationToken>());
        await room.DidNotReceive()
            .GetParameterValueAsync(Parameter.HasCommandAutoCorrect, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotReplyAutoCorrect_WhenDisabledInRoom()
    {
        // Arrange
        var commandName = "hep";
        var room = Substitute.For<IRoom>();
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(false);
        _addedCommandsManager.TryExecuteAddedCommand(commandName, _context, Arg.Any<CancellationToken>()).Returns(false);
        _context.IsPrivateMessage.Returns(false);
        _context.Room.Returns(room);
        room.GetParameterValueAsync(Parameter.HasCommandAutoCorrect, Arg.Any<CancellationToken>())
            .Returns("false");

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Assert
        _context.DidNotReceive()
            .ReplyLocalizedMessage("command_autocorrect_suggestion", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotReplyAutoCorrect_WhenAddedCommandHandled()
    {
        // Arrange
        var commandName = "customCommand";
        var room = Substitute.For<IRoom>();
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(false);
        _addedCommandsManager.TryExecuteAddedCommand(commandName, _context, Arg.Any<CancellationToken>()).Returns(true);
        _context.IsPrivateMessage.Returns(false);
        _context.Room.Returns(room);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Assert
        _context.DidNotReceive()
            .ReplyLocalizedMessage("command_autocorrect_suggestion", Arg.Any<object[]>());
        await room.DidNotReceive()
            .GetParameterValueAsync(Parameter.HasCommandAutoCorrect, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldRunCommand_WhenIsPrivateMessageAndCommandHasRoomRestriction()
    {
        // Arrange
        var commandName = "restrictedCommand";
        var command = Substitute.For<ICommand>();
        var runSignal = new TaskCompletionSource<bool>();
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(true);
        _dependencyContainerService.ResolveNamed<ICommand>(commandName).Returns(command);
        _context.IsPrivateMessage.Returns(true);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.RoomRestriction.Returns(["someroom"]);
        _context.RoomId.Returns("otherroom");
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await Task.WhenAny(runSignal.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        // Assert
        await command.Received(1).RunAsync(_context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotRunCommand_WhenNotPrivateMessageAndRoomNotInRestriction()
    {
        // Arrange
        var commandName = "restrictedCommand";
        var command = Substitute.For<ICommand>();
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(true);
        _dependencyContainerService.ResolveNamed<ICommand>(commandName).Returns(command);
        _context.IsPrivateMessage.Returns(false);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.RoomRestriction.Returns(["someroom"]);
        _context.RoomId.Returns("otherroom");

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Assert
        await command.DidNotReceive().RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Test_TryCancel_ShouldReturnFalse_WhenCommandNotFound()
    {
        // Act
        var result = _commandExecutor.TryCancel(Guid.NewGuid());

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldDoNothing_WhenCommandNotFoundAndIsPrivateMessage()
    {
        // Arrange
        var commandName = "nonExistentCommand";
        var context = Substitute.For<IContext>();
        context.IsPrivateMessage.Returns(true);
        _dependencyContainerService.IsRegisteredWithName<ICommand>(commandName).Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, context);

        // Assert
        await _addedCommandsManager.DidNotReceive().TryExecuteAddedCommand(commandName, context, Arg.Any<CancellationToken>());
        _dependencyContainerService.DidNotReceive().ResolveNamed<ICommand>(commandName);
    }
}
