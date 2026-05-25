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
public class PingCommandIntegrationTest
{
    private const string FRANAIS_ROOM_ID = "franais";
    private const string BOT_DEVELOPMENT_ROOM_ID = "botdevelopment";

    private IClient _client;
    private Bot _bot;
    private IContainer _container;
    private DependencyContainerService _dependencyContainerService;

    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IClient>();
        var clockService = Substitute.For<IClockService>();
        var roomsManager = Substitute.For<IRoomsManager>();
        var systemService = Substitute.For<ISystemService>();
        var startManager = Substitute.For<IStartManager>();
        var configuration = Substitute.For<IConfiguration>();
        var resourcesService = Substitute.For<IResourcesService>();
        var userDetailsManager = Substitute.For<IUserDetailsManager>();
        var addedCommandsManager = Substitute.For<IAddedCommandsManager>();
        var playTimeUpdateService = Substitute.For<IPlayTimeUpdateService>();
        _dependencyContainerService = new DependencyContainerService();

        clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        configuration.Trigger.Returns("-");
        configuration.RoomBlacklist.Returns(Array.Empty<string>());
        configuration.DefaultRoom.Returns(FRANAIS_ROOM_ID);
        configuration.DefaultLocaleCode.Returns("");
        configuration.Whitelist.Returns(Array.Empty<string>());

        var voicedUser = Substitute.For<IUser>();
        voicedUser.UserId.Returns("voiceduser");
        voicedUser.Name.Returns("VoicedUser");
        voicedUser.Rank.Returns(Rank.Voiced);

        var regularUser = Substitute.For<IUser>();
        regularUser.UserId.Returns("regularuser");
        regularUser.Name.Returns("RegularUser");
        regularUser.Rank.Returns(Rank.Regular);

        var panur = Substitute.For<IUser>();
        panur.UserId.Returns("panur");
        panur.Name.Returns("Panur");
        panur.Rank.Returns(Rank.Voiced);

        var franaisRoom = Substitute.For<IRoom>();
        franaisRoom.RoomId.Returns(FRANAIS_ROOM_ID);
        franaisRoom.Users.Returns(new Dictionary<string, IUser>
        {
            ["voiceduser"] = voicedUser,
            ["regularuser"] = regularUser
        });

        var botDevelopmentRoom = Substitute.For<IRoom>();
        botDevelopmentRoom.RoomId.Returns(BOT_DEVELOPMENT_ROOM_ID);
        botDevelopmentRoom.Users.Returns(new Dictionary<string, IUser>
        {
            ["panur"] = panur
        });

        roomsManager.HasRoom(Arg.Any<string>()).Returns(true);
        roomsManager.GetRoom(FRANAIS_ROOM_ID).Returns(franaisRoom);
        roomsManager.GetRoom(BOT_DEVELOPMENT_ROOM_ID).Returns(botDevelopmentRoom);

        var telemetry = Substitute.For<ITelemetryService>();
        var handlerManager = new HandlerManager(_dependencyContainerService, telemetry);
        _bot = new Bot(_client, clockService, roomsManager, handlerManager,
            systemService, startManager, playTimeUpdateService, telemetry);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(_dependencyContainerService).As<IDependencyContainerService>();
        builder.RegisterInstance(_bot).As<IBot>();
        builder.RegisterInstance(roomsManager).As<IRoomsManager>();
        builder.RegisterInstance(configuration).As<IConfiguration>();
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
        builder.RegisterCommand<TestPingCommand>();

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
    public async Task Test_PingCommand_ShouldNotReply_WhenUserHasInsufficientRank()
    {
        const string message = $">{FRANAIS_ROOM_ID}\n|c:|1| RegularUser|-ping";

        await _bot.HandleReceivedMessageAsync(message);
        await Task.Delay(200);

        _client.DidNotReceive().Send(Arg.Any<string>());
    }

    [Test]
    public async Task Test_PingCommand_ShouldReplyInRoom_WhenVoicedUserSendsPing()
    {
        const string message = $">{FRANAIS_ROOM_ID}\n|c:|1|+VoicedUser|-ping";

        await _bot.HandleReceivedMessageAsync(message);

        var probe = _container.Resolve<ICommandExecutionProbe>();
        await probe.WaitAsync(TimeSpan.FromSeconds(2));

        _client.Received(1).Send($"{FRANAIS_ROOM_ID}|pong");
    }

    [Test]
    public async Task Test_PingCommand_ShouldReplyInCorrectRoom_WhenCommandSentFromDifferentRoom()
    {
        const string message = $">{BOT_DEVELOPMENT_ROOM_ID}\n|c:|1|+Panur|-ping";

        await _bot.HandleReceivedMessageAsync(message);

        var probe = _container.Resolve<ICommandExecutionProbe>();
        await probe.WaitAsync(TimeSpan.FromSeconds(2));

        _client.Received(1).Send($"{BOT_DEVELOPMENT_ROOM_ID}|pong");
    }
}

[NamedCommand("ping")]
internal sealed class TestPingCommand : Command
{
    private readonly ICommandExecutionProbe _probe;

    public TestPingCommand(ICommandExecutionProbe probe)
    {
        _probe = probe;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Voiced;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        context.Reply("pong", rankAware: true);
        _probe.MarkExecuted("pong");
        return Task.CompletedTask;
    }
}
