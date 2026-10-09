using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Telemetry;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Handlers;

public class HandlerManagerTests
{
    private List<IHandler> _registeredHandlers;
    private ITelemetryService _telemetryService;
    private HandlerManager _handlerManager;

    [SetUp]
    public void SetUp()
    {
        _registeredHandlers = [];
        _telemetryService = Substitute.For<ITelemetryService>();
        _handlerManager = new HandlerManager(new Lazy<IEnumerable<IHandler>>(() => _registeredHandlers), _telemetryService);
    }

    [Test]
    public void Test_Initialize_ShouldPopulateHandlers_WhenHandlersAreResolved()
    {
        // Arrange
        var handler1 = Substitute.For<IHandler>();
        handler1.Identifier.Returns("Handler1");
        var handler2 = Substitute.For<IHandler>();
        handler2.Identifier.Returns("Handler2");

        _registeredHandlers.AddRange(new[] { handler1, handler2 });

        // Act
        _handlerManager.Initialize();

        // Assert
        Assert.That(_handlerManager.IsInitialized, Is.True);
    }

    [Test]
    public void Test_Initialize_ShouldSetIsInitializedTrue_WhenCalled()
    {
        // Arrange
        var handler = Substitute.For<IHandler>();
        _registeredHandlers.AddRange(new[] { handler });

        // Act
        _handlerManager.Initialize();

        // Assert
        Assert.That(_handlerManager.IsInitialized, Is.True);
    }

    [Test]
    public async Task Test_HandleMessage_ShouldInvokeOnMessageReceivedOnEnabledHandlers_WhenCalled()
    {
        // Arrange
        var handler1 = Substitute.For<IHandler>();
        handler1.IsEnabled.Returns(true);
        handler1.HandledMessageTypes.Returns((IReadOnlySet<string>)null);
        var handler2 = Substitute.For<IHandler>();
        handler2.IsEnabled.Returns(false);
        handler2.HandledMessageTypes.Returns((IReadOnlySet<string>)null);
        var handler3 = Substitute.For<IHandler>();
        handler3.IsEnabled.Returns(true);
        handler3.HandledMessageTypes.Returns((IReadOnlySet<string>)null);

        handler1.Identifier.Returns("Handler1");
        handler2.Identifier.Returns("Handler2");
        handler3.Identifier.Returns("Handler3");

        _registeredHandlers.AddRange(new[] { handler1, handler2, handler3 });
        _handlerManager.Initialize();

        var parts = new[] { "MessagePart1", "MessagePart2" };
        var roomId = "Room1";

        // Act
        await _handlerManager.HandleMessageAsync(parts, roomId);

        // Assert
        await handler1.Received(1).HandleReceivedMessageAsync(parts, roomId);
        await handler2.DidNotReceive().HandleReceivedMessageAsync(Arg.Any<string[]>(), Arg.Any<string>());
        await handler3.Received(1).HandleReceivedMessageAsync(parts, roomId);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldInvokeHandler_WhenHandledMessageTypesIsNull()
    {
        // Arrange
        var handler = Substitute.For<IHandler>();
        handler.IsEnabled.Returns(true);
        handler.Identifier.Returns("Handler1");
        handler.HandledMessageTypes.Returns((IReadOnlySet<string>)null);

        _registeredHandlers.AddRange(new[] { handler });
        _handlerManager.Initialize();

        var parts = new[] { "room", "anytype", "data" };

        // Act
        await _handlerManager.HandleMessageAsync(parts, "room1");

        // Assert
        await handler.Received(1).HandleReceivedMessageAsync(parts, "room1");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldInvokeHandler_WhenMessageTypeMatchesHandledMessageTypes()
    {
        // Arrange
        var handler = Substitute.For<IHandler>();
        handler.IsEnabled.Returns(true);
        handler.Identifier.Returns("Handler1");
        handler.HandledMessageTypes.Returns(new HashSet<string> { "challstr" });

        _registeredHandlers.AddRange(new[] { handler });
        _handlerManager.Initialize();

        var parts = new[] { "room", "challstr", "data" };

        // Act
        await _handlerManager.HandleMessageAsync(parts, "room1");

        // Assert
        await handler.Received(1).HandleReceivedMessageAsync(parts, "room1");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotInvokeHandler_WhenMessageTypeDoesNotMatchHandledMessageTypes()
    {
        // Arrange
        var handler = Substitute.For<IHandler>();
        handler.IsEnabled.Returns(true);
        handler.Identifier.Returns("Handler1");
        handler.HandledMessageTypes.Returns(new HashSet<string> { "challstr" });

        _registeredHandlers.AddRange(new[] { handler });
        _handlerManager.Initialize();

        var parts = new[] { "room", "updateuser", "data" };

        // Act
        await _handlerManager.HandleMessageAsync(parts, "room1");

        // Assert
        await handler.DidNotReceive().HandleReceivedMessageAsync(Arg.Any<string[]>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotInvokeHandler_WhenPartsHasNoMessageTypeAndHandledMessageTypesIsSet()
    {
        // Arrange
        var handler = Substitute.For<IHandler>();
        handler.IsEnabled.Returns(true);
        handler.Identifier.Returns("Handler1");
        handler.HandledMessageTypes.Returns(new HashSet<string> { "challstr" });

        _registeredHandlers.AddRange(new[] { handler });
        _handlerManager.Initialize();

        var parts = new[] { "room" };

        // Act
        await _handlerManager.HandleMessageAsync(parts, "room1");

        // Assert
        await handler.DidNotReceive().HandleReceivedMessageAsync(Arg.Any<string[]>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldInvokeHandler_WhenPartsHasNoMessageTypeAndHandledMessageTypesIsNull()
    {
        // Arrange
        var handler = Substitute.For<IHandler>();
        handler.IsEnabled.Returns(true);
        handler.Identifier.Returns("Handler1");
        handler.HandledMessageTypes.Returns((IReadOnlySet<string>)null);

        _registeredHandlers.AddRange(new[] { handler });
        _handlerManager.Initialize();

        var parts = new[] { "room" };

        // Act
        await _handlerManager.HandleMessageAsync(parts, "room1");

        // Assert
        await handler.Received(1).HandleReceivedMessageAsync(parts, "room1");
    }
}