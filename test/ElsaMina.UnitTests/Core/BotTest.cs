using ElsaMina.Core;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.PlayTime;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Start;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Telemetry;
using NSubstitute;

namespace ElsaMina.UnitTests.Core;

public class BotTest
{
    private IClient _client;
    private IClockService _clockService;
    private IRoomsManager _roomsManager;
    private IHandlerManager _handlerManager;
    private ISystemService _systemService;
    private IStartManager _startManager;
    private IPlayTimeUpdateService _playTimeUpdateService;
    private ITelemetryService _telemetryService;

    private Bot _bot;

    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IClient>();
        _clockService = Substitute.For<IClockService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _handlerManager = Substitute.For<IHandlerManager>();
        _systemService = Substitute.For<ISystemService>();
        _startManager = Substitute.For<IStartManager>();
        _playTimeUpdateService = Substitute.For<IPlayTimeUpdateService>();
        _telemetryService = Substitute.For<ITelemetryService>();

        _bot = new Bot(_client, _clockService, _roomsManager, _handlerManager, _systemService,
            new BotLifecycleService(_startManager, _playTimeUpdateService), _telemetryService);
    }

    [Test]
    public async Task Test_StartAsync_ShouldConnectAndCallLifecycleHandler()
    {
        // Act
        await _bot.StartAsync();

        // Assert
        await _startManager.Received(1).LoadStaticDataAsync(Arg.Any<CancellationToken>());
        await _client.Received(1).Connect();
    }

    [Test]
    public void Test_OnDisconnect_ShouldCallLifecycleHandler()
    {
        // Act
        _bot.OnDisconnect();

        // Assert
        _roomsManager.Received(1).Clear();
    }

    [Test]
    public void Test_OnExit_ShouldProcessPendingPlayTimeUpdates()
    {
        // Act
        _bot.OnExit();

        // Assert
        _playTimeUpdateService.Received(1).ProcessPendingPlayTimeUpdatesAsync();
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldInitializeRooms_WhenARoomIsReceived()
    {
        // Arrange
        const string message = ">room\n|init|chat\n|title|Room Title\n|users|5,*Bot,@Mod, Regular,#Ro User,+Voiced\n";

        // Act
        await _bot.HandleReceivedMessageAsync(message);

        // Assert
        string[] expectedLines =
            [">room", "|init|chat", "|title|Room Title", "|users|5,*Bot,@Mod, Regular,#Ro User,+Voiced"];
        await _roomsManager.Received(1).InitializeRoomAsync("room",
            Arg.Is<IEnumerable<string>>(users => users.SequenceEqual(expectedLines)), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldInitializeHandlers_WhenHandlersAreNotInitialized()
    {
        // Arrange
        const string message = "|c:|1|%Earth|test";
        _handlerManager.IsInitialized.Returns(false);

        // Act
        await _bot.HandleReceivedMessageAsync(message);

        // Assert
        _handlerManager.Received(1).Initialize();
        string[] expectedParts = ["", "c:", "1", "%Earth", "test"];
        await _handlerManager.Received(1)
            .HandleMessageAsync(Arg.Is<string[]>(parts => parts.SequenceEqual(expectedParts)),
                cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldCallHandlers_WhenHandlersAreInitialized()
    {
        // Arrange
        const string message = "|c:|1|%Earth|test";
        _handlerManager.IsInitialized.Returns(true);

        // Act
        await _bot.HandleReceivedMessageAsync(message);

        // Assert
        _handlerManager.DidNotReceive().Initialize();
        string[] expectedParts = ["", "c:", "1", "%Earth", "test"];
        await _handlerManager.Received(1).HandleMessageAsync(
            Arg.Is<string[]>(parts => parts.SequenceEqual(expectedParts)),
            cancellationToken: Arg.Any<CancellationToken>());
    }
}