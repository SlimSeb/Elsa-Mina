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
public class BotChatCommandIntegrationTest
{
    private const string ROOM_ID = "lobby";

    private IClient _client;
    private IRoom _room;
    private IRoomsManager _roomsManager;
    private IConfiguration _configuration;
    private DependencyContainerService _dependencyContainerService;
    private IContainer _container;
    private Bot _bot;

    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IClient>();
        var clockService = Substitute.For<IClockService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        var systemService = Substitute.For<ISystemService>();
        var startManager = Substitute.For<IStartManager>();
        _configuration = Substitute.For<IConfiguration>();
        var resourcesService = Substitute.For<IResourcesService>();
        var userDetailsManager = Substitute.For<IUserDetailsManager>();
        var addedCommandsManager = Substitute.For<IAddedCommandsManager>();
        var playTimeUpdateService = Substitute.For<IPlayTimeUpdateService>();
        _dependencyContainerService = new DependencyContainerService();

        clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _configuration.Trigger.Returns("!");
        _configuration.RoomBlacklist.Returns(Array.Empty<string>());
        _configuration.DefaultRoom.Returns(ROOM_ID);
        _configuration.DefaultLocaleCode.Returns("");
        _configuration.Whitelist.Returns(Array.Empty<string>());

        var voicedUser = Substitute.For<IUser>();
        voicedUser.UserId.Returns("earth");
        voicedUser.Name.Returns("Earth");
        voicedUser.Rank.Returns(Rank.Voiced);

        var regularUser = Substitute.For<IUser>();
        regularUser.UserId.Returns("mec");
        regularUser.Name.Returns("Mec");
        regularUser.Rank.Returns(Rank.Regular);

        _room = Substitute.For<IRoom>();
        _room.RoomId.Returns(ROOM_ID);
        _room.Users.Returns(new Dictionary<string, IUser>
        {
            ["earth"] = voicedUser,
            ["mec"] = regularUser
        });

        _roomsManager.HasRoom(Arg.Any<string>()).Returns(true);
        _roomsManager.GetRoom(ROOM_ID).Returns(_room);

        var telemetry = Substitute.For<ITelemetryService>();
        var handlerManager = new HandlerManager(_dependencyContainerService, telemetry);
        _bot = new Bot(_client, clockService, _roomsManager, handlerManager,
            systemService, startManager, playTimeUpdateService, telemetry);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(_dependencyContainerService).As<IDependencyContainerService>();
        builder.RegisterInstance(_bot).As<IBot>();
        builder.RegisterInstance(_roomsManager).As<IRoomsManager>();
        builder.RegisterInstance(_configuration).As<IConfiguration>();
        builder.RegisterInstance(resourcesService).As<IResourcesService>();
        builder.RegisterInstance(userDetailsManager).As<IUserDetailsManager>();
        builder.RegisterInstance(addedCommandsManager).As<IAddedCommandsManager>();
        builder.RegisterInstance(telemetry).As<ITelemetryService>();
        builder.RegisterType<PmSendersManager>().As<IPmSendersManager>().SingleInstance();
        builder.RegisterType<ContextFactory>().As<IContextFactory>().SingleInstance();
        builder.RegisterType<FeatureSwitchService>().As<IFeatureSwitchService>().SingleInstance();
        builder.RegisterType<CommandExecutor>().As<ICommandExecutor>().SingleInstance();
        builder.RegisterType<CommandExecutionProbe>().As<ICommandExecutionProbe>().SingleInstance();
        builder.RegisterHandler<ChatMessageCommandHandler>();
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
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_WhenChatCommandFromVoicedUser_ShouldReplyInRoom()
    {
        // Arrange
        const string receivedMessage = ">lobby\n|c:|1234567890|+Earth|!echo hello world";

        // Act
        await _bot.HandleReceivedMessageAsync(receivedMessage);

        // Assert
        var probe = _container.Resolve<ICommandExecutionProbe>();
        var commandOutput = await probe.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(commandOutput, Is.EqualTo("echo:hello world"));
        _client.Received(1).Send("lobby|echo:hello world");
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_WhenChatCommandFromRegularUser_ShouldReplyAsPm()
    {
        // Arrange
        const string receivedMessage = ">lobby\n|c:|1234567890| Mec|!echo hello world";

        // Act
        await _bot.HandleReceivedMessageAsync(receivedMessage);

        // Assert
        var probe = _container.Resolve<ICommandExecutionProbe>();
        await probe.WaitAsync(TimeSpan.FromSeconds(2));

        _client.Received(1).Send("lobby|/pm mec, echo:hello world");
    }
}
