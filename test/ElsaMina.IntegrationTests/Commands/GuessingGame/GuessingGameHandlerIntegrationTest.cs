using Autofac;
using ElsaMina.Commands.Games.GuessingGame;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.AddedCommands;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.PlayTime;
using ElsaMina.Core.Services.PrivateMessages;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Start;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Telemetry;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.Core.Utils;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.GuessingGame;

[TestFixture]
public class GuessingGameHandlerIntegrationTest
{
    private const string ROOM_ID = "lobby";

    private IClient _client;
    private IRoom _room;
    private IRoomsManager _roomsManager;
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
        var configuration = Substitute.For<IConfiguration>();
        var resourcesService = Substitute.For<IResourcesService>();
        var userDetailsManager = Substitute.For<IUserDetailsManager>();
        var addedCommandsManager = Substitute.For<IAddedCommandsManager>();
        var playTimeUpdateService = Substitute.For<IPlayTimeUpdateService>();
        _dependencyContainerService = new DependencyContainerService();

        clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        configuration.Trigger.Returns("!");
        configuration.RoomBlacklist.Returns(Array.Empty<string>());
        configuration.DefaultRoom.Returns(ROOM_ID);
        configuration.DefaultLocaleCode.Returns("");
        configuration.Whitelist.Returns(Array.Empty<string>());

        var user = Substitute.For<IUser>();
        user.UserId.Returns("earth");
        user.Name.Returns("Earth");
        user.Rank.Returns(Rank.Voiced);

        _room = Substitute.For<IRoom>();
        _room.RoomId.Returns(ROOM_ID);
        _room.Users.Returns(new Dictionary<string, IUser> { ["earth"] = user });

        _roomsManager.HasRoom(Arg.Any<string>()).Returns(true);
        _roomsManager.GetRoom(ROOM_ID).Returns(_room);

        var telemetry = Substitute.For<ITelemetryService>();
        var handlerManager = new HandlerManager(_dependencyContainerService, telemetry);
        _bot = new Bot(_client, clockService, _roomsManager, handlerManager,
            systemService, new BotLifecycleService(startManager, playTimeUpdateService), telemetry);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(_dependencyContainerService).As<IDependencyContainerService>();
        builder.RegisterInstance(_bot).As<IBot>();
        builder.RegisterInstance(_roomsManager).As<IRoomsManager>();
        builder.RegisterInstance(configuration).As<IConfiguration>();
        builder.RegisterInstance(resourcesService).As<IResourcesService>();
        builder.RegisterInstance(userDetailsManager).As<IUserDetailsManager>();
        builder.RegisterInstance(addedCommandsManager).As<IAddedCommandsManager>();
        builder.RegisterType<PmSendersManager>().As<IPmSendersManager>().SingleInstance();
        builder.RegisterType<ContextFactory>().As<IContextFactory>().SingleInstance();
        builder.RegisterHandler<GuessingGameHandler>();

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
    public async Task Test_HandleReceivedMessageAsync_WhenGuessingGameIsActive_ShouldRouteAnswerToGame()
    {
        // Arrange
        var game = Substitute.For<IGame, IGuessingGame>();
        _room.Game.Returns(game);

        const string receivedMessage = ">lobby\n|c:|1234567890|+Earth|pikachu";

        // Act
        await _bot.HandleReceivedMessageAsync(receivedMessage);

        // Assert
        ((IGuessingGame)game).Received(1).OnAnswer("Earth", "pikachu");
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_WhenNoGameIsActive_ShouldNotCallOnAnswer()
    {
        // Arrange
        var game = Substitute.For<IGame, IGuessingGame>();
        _room.Game.Returns((IGame)null);

        const string receivedMessage = ">lobby\n|c:|1234567890|+Earth|pikachu";

        // Act
        await _bot.HandleReceivedMessageAsync(receivedMessage);

        // Assert
        ((IGuessingGame)game).DidNotReceive().OnAnswer(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_WhenGameIsActiveButRoomIsWrong_ShouldNotCallOnAnswer()
    {
        // Arrange
        var game = Substitute.For<IGame, IGuessingGame>();
        _room.Game.Returns(game);
        _roomsManager.GetRoom("otherroom").Returns((IRoom)null);

        const string receivedMessage = ">otherroom\n|c:|1234567890|+Earth|pikachu";

        // Act
        await _bot.HandleReceivedMessageAsync(receivedMessage);

        // Assert
        ((IGuessingGame)game).DidNotReceive().OnAnswer(Arg.Any<string>(), Arg.Any<string>());
    }
}
