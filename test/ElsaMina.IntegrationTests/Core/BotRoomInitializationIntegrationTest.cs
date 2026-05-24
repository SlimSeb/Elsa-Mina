using Autofac;
using ElsaMina.Core;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.PlayTime;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Start;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Telemetry;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Core;

[TestFixture]
public class BotRoomInitializationIntegrationTest
{
    private IRoomsManager _roomsManager;
    private Bot _bot;
    private IContainer _container;

    [SetUp]
    public void SetUp()
    {
        var client = Substitute.For<IClient>();
        var clockService = Substitute.For<IClockService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        var systemService = Substitute.For<ISystemService>();
        var startManager = Substitute.For<IStartManager>();
        var configuration = Substitute.For<IConfiguration>();
        var playTimeUpdateService = Substitute.For<IPlayTimeUpdateService>();
        var dependencyContainerService = new DependencyContainerService();

        clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        configuration.Trigger.Returns("!");
        configuration.RoomBlacklist.Returns(Array.Empty<string>());
        configuration.DefaultLocaleCode.Returns("");

        var telemetry = Substitute.For<ITelemetryService>();
        var handlerManager = new HandlerManager(dependencyContainerService, telemetry);
        _bot = new Bot(client, clockService, _roomsManager, handlerManager,
            systemService, startManager, playTimeUpdateService, telemetry);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(dependencyContainerService).As<IDependencyContainerService>();
        _container = builder.Build();
        dependencyContainerService.SetContainer(_container);
    }

    [TearDown]
    public void TearDown()
    {
        _bot?.Dispose();
        _container?.Dispose();
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_WhenRoomInitMessageReceived_ShouldCallInitializeRoomAsync()
    {
        // Arrange
        const string receivedMessage = ">lobby\n|init|chat\n|title|Lobby\n|users|2,+Earth, Mec";

        // Act
        await _bot.HandleReceivedMessageAsync(receivedMessage);

        // Assert
        await _roomsManager.Received(1).InitializeRoomAsync(
            "lobby",
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_WhenRoomInitMessageReceived_ShouldPassAllLinesToInitialize()
    {
        // Arrange
        const string receivedMessage = ">lobby\n|init|chat\n|title|Lobby\n|users|2,+Earth, Mec";
        IEnumerable<string> capturedLines = null;
        await _roomsManager.InitializeRoomAsync(
            Arg.Any<string>(),
            Arg.Do<IEnumerable<string>>(lines => capturedLines = lines),
            Arg.Any<CancellationToken>());

        // Act
        await _bot.HandleReceivedMessageAsync(receivedMessage);

        // Assert
        Assert.That(capturedLines, Is.Not.Null);
        var linesList = capturedLines.ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(linesList, Does.Contain("|init|chat"));
            Assert.That(linesList, Does.Contain("|title|Lobby"));
            Assert.That(linesList, Does.Contain("|users|2,+Earth, Mec"));
        }
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_WhenNonInitMessageReceived_ShouldNotCallInitializeRoomAsync()
    {
        // Arrange
        const string receivedMessage = ">lobby\n|c:|1234567890|+Earth|hello";

        // Act
        await _bot.HandleReceivedMessageAsync(receivedMessage);

        // Assert
        await _roomsManager.DidNotReceive().InitializeRoomAsync(
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<CancellationToken>());
    }
}
