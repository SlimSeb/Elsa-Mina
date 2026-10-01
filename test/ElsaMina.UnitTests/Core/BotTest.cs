using ElsaMina.Core;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Dispatch;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Telemetry;
using NSubstitute;

namespace ElsaMina.UnitTests.Core;

public class BotTest
{
    private IClient _client;
    private IClockService _clockService;
    private IRoomsManager _roomsManager;
    private IHandlerManager _handlerManager;
    private IOutgoingMessageQueue _outgoingMessageQueue;
    private IBotLifecycleService _botLifecycleService;
    private ITelemetryService _telemetryService;

    private Bot _bot;

    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IClient>();
        _clockService = Substitute.For<IClockService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _handlerManager = Substitute.For<IHandlerManager>();
        _outgoingMessageQueue = Substitute.For<IOutgoingMessageQueue>();
        _botLifecycleService = Substitute.For<IBotLifecycleService>();
        _telemetryService = Substitute.For<ITelemetryService>();

        _bot = new Bot(_client, _clockService, _roomsManager, _handlerManager, _outgoingMessageQueue,
            _botLifecycleService, _telemetryService);
    }

    [TearDown]
    public void TearDown()
    {
        _bot.Dispose();
    }

    [Test]
    public async Task Test_StartAsync_ShouldInitializeHandlersRunLifecycleAndConnect()
    {
        // Act
        await _bot.StartAsync();

        // Assert
        Received.InOrder(() =>
        {
            _handlerManager.Initialize();
            _botLifecycleService.OnStartingAsync(Arg.Any<CancellationToken>());
            _client.Connect();
        });
    }

    [Test]
    public void Test_OnDisconnect_ShouldClearRooms()
    {
        // Act
        _bot.OnDisconnect();

        // Assert
        _roomsManager.Received(1).Clear();
    }

    [Test]
    public async Task Test_StopAsync_ShouldRunExitLifecycleThenFlushOutgoingMessages()
    {
        // Act
        await _bot.StopAsync();

        // Assert
        Received.InOrder(() =>
        {
            _botLifecycleService.OnExitingAsync(Arg.Any<CancellationToken>());
            _outgoingMessageQueue.FlushAsync(Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public void Test_StopAsync_ShouldNotThrow_WhenShutdownTimesOut()
    {
        // Arrange
        _botLifecycleService.OnExitingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled(new CancellationToken(true)));

        // Act & Assert
        Assert.DoesNotThrowAsync(() => _bot.StopAsync());
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
        await _handlerManager.DidNotReceiveWithAnyArgs().HandleMessageAsync(default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldInitializeLobby_WhenInitHasNoRoomHeader()
    {
        // Arrange
        const string message = "|init|chat\n|title|Lobby\n|users|1,*Bot\n";

        // Act
        await _bot.HandleReceivedMessageAsync(message);

        // Assert
        await _roomsManager.Received(1).InitializeRoomAsync("lobby", Arg.Any<IEnumerable<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldPassRoomFromHeader_WhenFrameHasRoomHeader()
    {
        // Arrange
        const string message = ">franais\n|c:|1|%Earth|test";

        // Act
        await _bot.HandleReceivedMessageAsync(message);

        // Assert
        string[] expectedParts = ["", "c:", "1", "%Earth", "test"];
        await _handlerManager.Received(1).HandleMessageAsync(
            Arg.Is<string[]>(parts => parts.SequenceEqual(expectedParts)), "franais", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAttributeToLobby_WhenFrameHasNoRoomHeader()
    {
        // Arrange
        await _bot.HandleReceivedMessageAsync(">franais\n|c:|1|%Earth|first");
        const string message = "|c:|1|%Earth|test";

        // Act
        await _bot.HandleReceivedMessageAsync(message);

        // Assert
        string[] expectedParts = ["", "c:", "1", "%Earth", "test"];
        await _handlerManager.Received(1).HandleMessageAsync(
            Arg.Is<string[]>(parts => parts.SequenceEqual(expectedParts)), "lobby", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotInitializeHandlers()
    {
        // Act
        await _bot.HandleReceivedMessageAsync("|c:|1|%Earth|test");

        // Assert
        _handlerManager.DidNotReceive().Initialize();
    }

    [Test]
    public void Test_Send_ShouldEnqueueMessage()
    {
        // Act
        _bot.Send("room|hello");

        // Assert
        _outgoingMessageQueue.Received(1).Enqueue("room|hello");
    }

    [Test]
    public void Test_Say_ShouldPrefixRoomId()
    {
        // Act
        _bot.Say("room", "hello");

        // Assert
        _outgoingMessageQueue.Received(1).Enqueue("room|hello");
    }

    [Test]
    public void Test_Send_ShouldThrow_WhenMessageIsTooLong()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _bot.Send(new string('a', 125_001)));
        _outgoingMessageQueue.DidNotReceiveWithAnyArgs().Enqueue(default);
    }
}
