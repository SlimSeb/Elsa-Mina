using Autofac;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Services.AddedCommands;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.PlayTime;
using ElsaMina.Core.Services.PrivateMessages;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.FeatureSwitches;
using ElsaMina.Core.Services.Start;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Telemetry;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.Core.Utils;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Core;

[TestFixture]
public class BotHandleReceivedMessageIntegrationTest
{
    private IClient _client;
    private IClockService _clockService;
    private IRoomsManager _roomsManager;
    private ISystemService _systemService;
    private IStartManager _startManager;
    private IPlayTimeUpdateService _playTimeUpdateService;
    private IConfiguration _configuration;
    private IResourcesService _resourcesService;
    private IUserDetailsManager _userDetailsManager;
    private IAddedCommandsManager _addedCommandsManager;
    private DependencyContainerService _dependencyContainerService;
    private IContainer _container;
    private Bot _bot;
    
    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IClient>();
        _clockService = Substitute.For<IClockService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _systemService = Substitute.For<ISystemService>();
        _startManager = Substitute.For<IStartManager>();
        _configuration = Substitute.For<IConfiguration>();
        _resourcesService = Substitute.For<IResourcesService>();
        _userDetailsManager = Substitute.For<IUserDetailsManager>();
        _addedCommandsManager = Substitute.For<IAddedCommandsManager>();
        _playTimeUpdateService = Substitute.For<IPlayTimeUpdateService>();
        _dependencyContainerService = new DependencyContainerService();

        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _roomsManager.HasRoom(Arg.Any<string>()).Returns(true);
        _configuration.Trigger.Returns("!");
        _configuration.RoomBlacklist.Returns(Array.Empty<string>());
        _configuration.DefaultRoom.Returns("lobby");
        _configuration.DefaultLocaleCode.Returns("");

        var telemetry = Substitute.For<ITelemetryService>();
        var handlerManager = new HandlerManager(_dependencyContainerService, telemetry);
        _bot = new Bot(
            _client,
            _clockService,
            _roomsManager,
            handlerManager,
            _systemService,
            _startManager,
            _playTimeUpdateService,
            telemetry);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(_dependencyContainerService).As<IDependencyContainerService>();
        builder.RegisterInstance(_bot).As<IBot>();
        builder.RegisterInstance(_roomsManager).As<IRoomsManager>();
        builder.RegisterInstance(_configuration).As<IConfiguration>();
        builder.RegisterInstance(_resourcesService).As<IResourcesService>();
        builder.RegisterInstance(_userDetailsManager).As<IUserDetailsManager>();
        builder.RegisterInstance(_addedCommandsManager).As<IAddedCommandsManager>();
        builder.RegisterInstance(telemetry).As<ITelemetryService>();

        builder.RegisterType<PmSendersManager>().As<IPmSendersManager>().SingleInstance();
        builder.RegisterType<ContextFactory>().As<IContextFactory>().SingleInstance();
        builder.RegisterType<FeatureSwitchService>().As<IFeatureSwitchService>().SingleInstance();
        builder.RegisterType<CommandExecutor>().As<ICommandExecutor>().SingleInstance();
        builder.RegisterType<CommandExecutionProbe>().As<ICommandExecutionProbe>().SingleInstance();
        builder.RegisterHandler<PrivateMessageCommandHandler>();
        builder.RegisterCommand<EchoCommand>();

        _container = builder.Build();
        _dependencyContainerService.SetContainer(_container);
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _container?.Dispose();
        _bot?.Dispose();
        _playTimeUpdateService?.Dispose();
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_WhenPmCommandIsReceived_ShouldSendExpectedOutput()
    {
        // Arrange
        const string receivedMessage = "|pm|+Earth|ElsaMina|!echo hello world";

        // Act
        await _bot.HandleReceivedMessageAsync(receivedMessage);

        // Assert
        var probe = _container.Resolve<ICommandExecutionProbe>();
        var commandOutput = await probe.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(commandOutput, Is.EqualTo("echo:hello world"));
        _client.Received(1).Send("|/pm earth, echo:hello world");
    }
}

internal interface ICommandExecutionProbe
{
    void MarkExecuted(string output);
    Task<string> WaitAsync(TimeSpan timeout);
}

internal sealed class CommandExecutionProbe : ICommandExecutionProbe
{
    private readonly TaskCompletionSource<string> _resultTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void MarkExecuted(string output)
    {
        _resultTaskCompletionSource.TrySetResult(output);
    }

    public async Task<string> WaitAsync(TimeSpan timeout)
    {
        var completedTask = await Task.WhenAny(_resultTaskCompletionSource.Task, Task.Delay(timeout));
        if (completedTask != _resultTaskCompletionSource.Task)
        {
            throw new TimeoutException("The command did not run within the expected timeout.");
        }

        return await _resultTaskCompletionSource.Task;
    }
}

[NamedCommand("echo")]
internal sealed class EchoCommand : Command
{
    private readonly ICommandExecutionProbe _probe;

    public EchoCommand(ICommandExecutionProbe probe)
    {
        _probe = probe;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var output = $"echo:{context.Target}";
        context.Reply(output, rankAware: true);
        _probe.MarkExecuted(output);
        return Task.CompletedTask;
    }
}
