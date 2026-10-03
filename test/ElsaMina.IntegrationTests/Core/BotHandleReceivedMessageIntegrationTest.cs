using Autofac;
using ElsaMina.Commands.CustomCommands;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dispatch;
using ElsaMina.Core.Services.FeatureSwitches;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.PrivateMessages;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
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
    private IConfiguration _configuration;
    private IResourcesService _resourcesService;
    private IUserDetailsManager _userDetailsManager;
    private IAddedCommandsManager _addedCommandsManager;
    private IContainer _container;
    private Bot _bot;
    
    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IClient>();
        _clockService = Substitute.For<IClockService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _systemService = Substitute.For<ISystemService>();
        _configuration = Substitute.For<IConfiguration>();
        _resourcesService = Substitute.For<IResourcesService>();
        _userDetailsManager = Substitute.For<IUserDetailsManager>();
        _addedCommandsManager = Substitute.For<IAddedCommandsManager>();

        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _roomsManager.HasRoom(Arg.Any<string>()).Returns(true);
        _configuration.Trigger.Returns("!");
        _configuration.RoomBlacklist.Returns(Array.Empty<string>());
        _configuration.DefaultRoom.Returns("lobby");
        _configuration.DefaultLocaleCode.Returns("");

        var telemetry = Substitute.For<ITelemetryService>();

        var builder = new ContainerBuilder();
        builder.RegisterInstance(_client).As<IClient>();
        builder.RegisterInstance(_clockService).As<IClockService>();
        builder.RegisterInstance(_systemService).As<ISystemService>();
        builder.RegisterInstance(Substitute.For<IBotLifecycleService>()).As<IBotLifecycleService>();
        builder.RegisterType<OutgoingMessageQueue>().As<IOutgoingMessageQueue>().SingleInstance();
        builder.RegisterType<HandlerManager>().As<IHandlerManager>().SingleInstance();
        builder.RegisterType<CommandRegistry>().As<ICommandRegistry>().SingleInstance();
        builder.RegisterType<Bot>().As<IBot>().AsSelf().SingleInstance();
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
        _bot = _container.Resolve<Bot>();
        _container.Resolve<IHandlerManager>().Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _container?.Dispose();
        _bot?.Dispose();
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
