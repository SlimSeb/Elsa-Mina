using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.FeatureSwitches;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Telemetry;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Core.Services.Commands;

public class CommandExecutorTest
{
    private ICommandRegistry _commandRegistry;
    private IDynamicCommandProvider _dynamicCommandProvider;
    private ITelemetryService _telemetryService;
    private IFeatureSwitchService _featureSwitchService;
    private CommandExecutor _commandExecutor;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _commandRegistry = Substitute.For<ICommandRegistry>();
        _commandRegistry.Find(Arg.Any<string>()).ReturnsNull();
        _commandRegistry.Commands.Returns([]);
        _dynamicCommandProvider = Substitute.For<IDynamicCommandProvider>();
        _telemetryService = Substitute.For<ITelemetryService>();
        _featureSwitchService = Substitute.For<IFeatureSwitchService>();
        _featureSwitchService.IsFeatureEnabled(Arg.Any<string>()).Returns(true);
        _context = Substitute.For<IContext>();
        _commandExecutor = new CommandExecutor(_commandRegistry,
            [_dynamicCommandProvider], _telemetryService, _featureSwitchService);
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
        expectedCommands[0].Name.Returns("1");
        expectedCommands[1].Name.Returns("2");
        _commandRegistry.Commands.Returns(expectedCommands);

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
        _commandRegistry.Commands.Returns([command1, command2, duplicateCommand]);

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
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();
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
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.RoomRestriction.Returns(["franais"]);
        _context.RoomId.Returns(roomId);
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();
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
        _commandRegistry.Find(commandName).Returns(command);
        _context.IsPrivateMessage.Returns(true);
        command.IsPrivateMessageOnly.Returns(false);
        command.IsAllowedInPrivateMessage.Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        await command.DidNotReceive().RunAsync(_context);
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotRunCommand_WhenNotWhitelisted()
    {
        // Arrange
        var commandName = "whitelistCommand";
        var command = Substitute.For<ICommand>();
        _commandRegistry.Find(commandName).Returns(command);
        command.IsWhitelistOnly.Returns(true);
        _context.IsSenderWhitelisted.Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        await command.DidNotReceive().RunAsync(_context);
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotRunCommand_WhenRankIsTooLow()
    {
        // Arrange
        var commandName = "rankedCommand";
        var command = Substitute.For<ICommand>();
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

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
        _commandRegistry.Find(commandName).Returns(command);
        _context.IsPrivateMessage.Returns(true);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();
        await Task.WhenAny(runSignal.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        // Assert
        await command.Received(1).RunAsync(_context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldReportErrorToUserAndNotThrow_WhenCommandExecutionFails()
    {
        // Arrange
        var commandName = "failingCommand";
        var command = Substitute.For<ICommand>();
        var expectedException = new InvalidOperationException("boom");
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.RoomRestriction.Returns(Array.Empty<string>());
        command.Name.Returns(commandName);
        command.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(expectedException));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        await _context.Received(1).HandleErrorAsync(expectedException, Arg.Any<CancellationToken>());
        Assert.That(_commandExecutor.RunningCommands, Is.Empty);
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldReturnBeforeCommandCompletes_WhenCommandRunsInBackground()
    {
        // Arrange
        var commandName = "slowCommand";
        var command = Substitute.For<ICommand>();
        var release = new TaskCompletionSource();
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.RoomRestriction.Returns(Array.Empty<string>());
        command.RunsInMessageOrder.Returns(false);
        command.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()).Returns(release.Task);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.That(_commandExecutor.RunningCommands.Count(), Is.EqualTo(1));
        release.SetResult();
        await _commandExecutor.WhenAllCommandsCompletedAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(_commandExecutor.RunningCommands, Is.Empty);
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldWaitForCommand_WhenCommandRunsInMessageOrder()
    {
        // Arrange
        var commandName = "gameCommand";
        var command = Substitute.For<ICommand>();
        var release = new TaskCompletionSource();
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.RoomRestriction.Returns(Array.Empty<string>());
        command.RunsInMessageOrder.Returns(true);
        command.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()).Returns(release.Task);

        // Act
        var execution = _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await Task.Delay(50);
        var wasCompletedBeforeRelease = execution.IsCompleted;
        release.SetResult();
        await execution.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.That(wasCompletedBeforeRelease, Is.False);
    }

    [Test]
    public async Task Test_OnExitingAsync_ShouldWaitForRunningCommands()
    {
        // Arrange
        var commandName = "slowCommand";
        var command = Substitute.For<ICommand>();
        var release = new TaskCompletionSource();
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.RoomRestriction.Returns(Array.Empty<string>());
        command.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()).Returns(release.Task);
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);

        // Act
        var exiting = _commandExecutor.OnExitingAsync(CancellationToken.None);
        await Task.Delay(50);
        var wasCompletedBeforeRelease = exiting.IsCompleted;
        release.SetResult();
        await exiting.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.That(wasCompletedBeforeRelease, Is.False);
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldTryDynamicProvider_WhenNotRegisteredAndNotPrivateMessage()
    {
        // Arrange
        var commandName = "customCommand";
        _commandRegistry.Find(commandName).ReturnsNull();
        _context.IsPrivateMessage.Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        await _dynamicCommandProvider.Received(1).TryExecuteAsync(commandName, _context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldAskDynamicProviders_WhenCommandNotFoundAndIsPrivateMessage()
    {
        // Arrange
        var commandName = "missingCommand";
        _commandRegistry.Find(commandName).ReturnsNull();
        _context.IsPrivateMessage.Returns(true);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        await _dynamicCommandProvider.Received(1).TryExecuteAsync(commandName, _context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldAskDynamicProvider_WhenCommandNotRegisteredAndNotPrivateMessage()
    {
        // Arrange
        var commandName = "customCommand";
        var context = Substitute.For<IContext>();
        context.IsPrivateMessage.Returns(false);
        _commandRegistry.Find(commandName).ReturnsNull();

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        await _dynamicCommandProvider.Received().TryExecuteAsync(commandName, context, Arg.Any<CancellationToken>());
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
        _commandRegistry.Find(commandName).ReturnsNull();
        _commandRegistry.Commands.Returns([command]);
        _dynamicCommandProvider.TryExecuteAsync(commandName, _context, Arg.Any<CancellationToken>()).Returns(false);
        _context.IsPrivateMessage.Returns(false);
        _context.Room.Returns(room);
        room.GetParameterValueAsync(Parameter.HasCommandAutoCorrect, Arg.Any<CancellationToken>())
            .Returns("true");

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

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
        _commandRegistry.Find(commandName).ReturnsNull();
        _commandRegistry.Commands.Returns([command]);
        _context.IsPrivateMessage.Returns(true);
        _context.Room.Returns(room);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        _context.Received(1)
            .ReplyLocalizedMessage("command_autocorrect_suggestion", commandName, "help");
        await _dynamicCommandProvider.Received(1).TryExecuteAsync(commandName, _context, Arg.Any<CancellationToken>());
        await room.DidNotReceive()
            .GetParameterValueAsync(Parameter.HasCommandAutoCorrect, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotReplyAutoCorrect_WhenDisabledInRoom()
    {
        // Arrange
        var commandName = "hep";
        var room = Substitute.For<IRoom>();
        _commandRegistry.Find(commandName).ReturnsNull();
        _dynamicCommandProvider.TryExecuteAsync(commandName, _context, Arg.Any<CancellationToken>()).Returns(false);
        _context.IsPrivateMessage.Returns(false);
        _context.Room.Returns(room);
        room.GetParameterValueAsync(Parameter.HasCommandAutoCorrect, Arg.Any<CancellationToken>())
            .Returns("false");

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        _context.DidNotReceive()
            .ReplyLocalizedMessage("command_autocorrect_suggestion", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotReplyAutoCorrect_WhenDynamicProviderHandledCommand()
    {
        // Arrange
        var commandName = "customCommand";
        var room = Substitute.For<IRoom>();
        _commandRegistry.Find(commandName).ReturnsNull();
        _dynamicCommandProvider.TryExecuteAsync(commandName, _context, Arg.Any<CancellationToken>()).Returns(true);
        _context.IsPrivateMessage.Returns(false);
        _context.Room.Returns(room);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

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
        _commandRegistry.Find(commandName).Returns(command);
        _context.IsPrivateMessage.Returns(true);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.RoomRestriction.Returns(["someroom"]);
        _context.RoomId.Returns("otherroom");
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();
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
        _commandRegistry.Find(commandName).Returns(command);
        _context.IsPrivateMessage.Returns(false);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.RoomRestriction.Returns(["someroom"]);
        _context.RoomId.Returns("otherroom");

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

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
    public async Task Test_TryExecuteCommandAsync_ShouldOnlyAskDynamicProviders_WhenCommandNotFoundAndIsPrivateMessage()
    {
        // Arrange
        var commandName = "nonExistentCommand";
        var context = Substitute.For<IContext>();
        context.IsPrivateMessage.Returns(true);
        _commandRegistry.Find(commandName).ReturnsNull();

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        await _dynamicCommandProvider.Received(1).TryExecuteAsync(commandName, context, Arg.Any<CancellationToken>());
        _commandRegistry.Received(1).Find(commandName);
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotRunCommand_WhenMaydayActiveAndSenderNotWhitelisted()
    {
        // Arrange
        var commandName = "anyCommand";
        _featureSwitchService.IsMaydayActive.Returns(true);
        _context.IsSenderWhitelisted.Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        _commandRegistry.DidNotReceive().Find(Arg.Any<string>());
        await _dynamicCommandProvider.DidNotReceive().TryExecuteAsync(Arg.Any<string>(), Arg.Any<IContext>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldRunCommand_WhenMaydayActiveButSenderIsWhitelisted()
    {
        // Arrange
        var commandName = "anyCommand";
        var command = Substitute.For<ICommand>();
        var runSignal = new TaskCompletionSource<bool>();
        _featureSwitchService.IsMaydayActive.Returns(true);
        _context.IsSenderWhitelisted.Returns(true);
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();
        await Task.WhenAny(runSignal.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        // Assert
        await command.Received(1).RunAsync(_context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotRunCommand_WhenFeatureSwitchDisabledAndSenderNotWhitelisted()
    {
        // Arrange
        var commandName = "featureCommand";
        var command = Substitute.For<ICommand>();
        command.Category.Returns("SomeFeature");
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        _context.IsSenderWhitelisted.Returns(false);
        _featureSwitchService.IsFeatureEnabled("SomeFeature").Returns(false);

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        await command.DidNotReceive().RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldRunCommand_WhenFeatureSwitchDisabledButSenderIsWhitelisted()
    {
        // Arrange
        var commandName = "featureCommand";
        var command = Substitute.For<ICommand>();
        var runSignal = new TaskCompletionSource<bool>();
        command.Category.Returns("SomeFeature");
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        _context.IsSenderWhitelisted.Returns(true);
        _featureSwitchService.IsFeatureEnabled("SomeFeature").Returns(false);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();
        await Task.WhenAny(runSignal.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        // Assert
        await command.Received(1).RunAsync(_context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldRunCommand_WhenFeatureSwitchEnabled()
    {
        // Arrange
        var commandName = "featureCommand";
        var command = Substitute.For<ICommand>();
        var runSignal = new TaskCompletionSource<bool>();
        command.Category.Returns("SomeFeature");
        _commandRegistry.Find(commandName).Returns(command);
        _context.HasRankOrHigher(command.RequiredRank).Returns(true);
        _context.IsSenderWhitelisted.Returns(false);
        _featureSwitchService.IsFeatureEnabled("SomeFeature").Returns(true);
        command.IsAllowedInPrivateMessage.Returns(true);
        command.When(x => x.RunAsync(Arg.Any<IContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => runSignal.TrySetResult(true));

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();
        await Task.WhenAny(runSignal.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        // Assert
        await command.Received(1).RunAsync(_context, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldNotSuggestHiddenCommand_WhenAutoCorrectEnabled()
    {
        // Arrange
        var commandName = "hep";
        var room = Substitute.For<IRoom>();
        var hiddenCommand = Substitute.For<ICommand>();
        hiddenCommand.Name.Returns("help");
        hiddenCommand.Aliases.Returns(Array.Empty<string>());
        hiddenCommand.IsHidden.Returns(true);
        _commandRegistry.Find(commandName).ReturnsNull();
        _commandRegistry.Commands.Returns([hiddenCommand]);
        _dynamicCommandProvider.TryExecuteAsync(commandName, _context, Arg.Any<CancellationToken>()).Returns(false);
        _context.IsPrivateMessage.Returns(false);
        _context.Room.Returns(room);
        room.GetParameterValueAsync(Parameter.HasCommandAutoCorrect, Arg.Any<CancellationToken>())
            .Returns("true");

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        _context.DidNotReceive()
            .ReplyLocalizedMessage("command_autocorrect_suggestion", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_TryExecuteCommandAsync_ShouldOnlySuggestVisibleCommands_WhenAutoCorrectEnabled()
    {
        // Arrange
        var commandName = "hep";
        var room = Substitute.For<IRoom>();
        var visibleCommand = Substitute.For<ICommand>();
        visibleCommand.Name.Returns("help");
        visibleCommand.Aliases.Returns(Array.Empty<string>());
        visibleCommand.IsHidden.Returns(false);
        var hiddenCommand = Substitute.For<ICommand>();
        hiddenCommand.Name.Returns("heap");
        hiddenCommand.Aliases.Returns(Array.Empty<string>());
        hiddenCommand.IsHidden.Returns(true);
        _commandRegistry.Find(commandName).ReturnsNull();
        _commandRegistry.Commands.Returns([visibleCommand, hiddenCommand]);
        _dynamicCommandProvider.TryExecuteAsync(commandName, _context, Arg.Any<CancellationToken>()).Returns(false);
        _context.IsPrivateMessage.Returns(false);
        _context.Room.Returns(room);
        room.GetParameterValueAsync(Parameter.HasCommandAutoCorrect, Arg.Any<CancellationToken>())
            .Returns("true");

        // Act
        await _commandExecutor.TryExecuteCommandAsync(commandName, _context);
        await _commandExecutor.WhenAllCommandsCompletedAsync();

        // Assert
        _context.Received(1)
            .ReplyLocalizedMessage("command_autocorrect_suggestion", commandName, "help");
    }
}
