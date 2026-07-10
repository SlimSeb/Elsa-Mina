using ElsaMina.Commands.Arcade.Inscriptions;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Inscriptions;

public class ArcadeInscriptionsManagerTest
{
    private IBot _bot;
    private IRoomsManager _roomsManager;
    private IClockService _clockService;
    private ArcadeInscriptionsManager _manager;

    [SetUp]
    public void SetUp()
    {
        _bot = Substitute.For<IBot>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        _manager = new ArcadeInscriptionsManager(_bot, _roomsManager, _clockService);
    }

    [Test]
    public void Test_TryGetState_ShouldReturnFalse_WhenRoomHasNoState()
    {
        var found = _manager.TryGetState("room1", out var state);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.False);
            Assert.That(state, Is.Null);
        }
    }

    [Test]
    public void Test_HasActiveInscriptions_ShouldReturnFalse_WhenNoStateExists()
    {
        Assert.That(_manager.HasActiveInscriptions("room1"), Is.False);
    }

    [Test]
    public void Test_InitInscriptions_ShouldCreateActiveState()
    {
        // Act
        var state = _manager.InitInscriptions("room1", "My Tournament");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.IsActive, Is.True);
            Assert.That(state.Title, Is.EqualTo("My Tournament"));
            Assert.That(_manager.HasActiveInscriptions("room1"), Is.True);
            Assert.That(_manager.TryGetState("room1", out _), Is.True);
        }
    }

    [Test]
    public void Test_StopInscriptions_ShouldDeactivateState()
    {
        // Arrange
        _manager.InitInscriptions("room1", "My Tournament");

        // Act
        _manager.StopInscriptions("room1");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_manager.HasActiveInscriptions("room1"), Is.False);
            Assert.That(_manager.TryGetState("room1", out var state), Is.True);
            Assert.That(state.IsActive, Is.False);
        }
    }

    [Test]
    public void Test_StopInscriptions_ShouldNotThrow_WhenRoomHasNoState()
    {
        Assert.DoesNotThrow(() => _manager.StopInscriptions("unknown"));
    }

    [Test]
    public void Test_StartTimer_ShouldSetTimerEnd_BasedOnClock()
    {
        // Arrange
        var state = _manager.InitInscriptions("room1", "My Tournament");

        // Act
        _manager.StartTimer("room1", 30);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.TimerEnd, Is.EqualTo(new DateTimeOffset(2026, 1, 1, 12, 30, 0, TimeSpan.Zero)));
            Assert.That(state.TimerCts, Is.Not.Null);
        }
    }

    [Test]
    public void Test_StartTimer_ShouldDoNothing_WhenRoomHasNoState()
    {
        Assert.DoesNotThrow(() => _manager.StartTimer("unknown", 30));
    }

    [Test]
    public void Test_CancelTimer_ShouldClearTimerEndAndTokenSource()
    {
        // Arrange
        var state = _manager.InitInscriptions("room1", "My Tournament");
        _manager.StartTimer("room1", 30);

        // Act
        _manager.CancelTimer("room1");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.TimerEnd, Is.Null);
            Assert.That(state.TimerCts, Is.Null);
        }
    }

    [Test]
    public void Test_StopInscriptions_ShouldCancelRunningTimer()
    {
        // Arrange
        var state = _manager.InitInscriptions("room1", "My Tournament");
        _manager.StartTimer("room1", 30);

        // Act
        _manager.StopInscriptions("room1");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.TimerEnd, Is.Null);
            Assert.That(state.TimerCts, Is.Null);
            Assert.That(state.IsActive, Is.False);
        }
    }
}
