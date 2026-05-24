using ElsaMina.Commands.Development.LagTest;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.System;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Development.LagTest;

[TestFixture]
public class LagTestManagerTest
{
    private IClockService _clockService;
    private ISystemService _systemService;
    private LagTestManager _manager;

    [SetUp]
    public void SetUp()
    {
        _clockService = Substitute.For<IClockService>();
        _systemService = Substitute.For<ISystemService>();

        _manager = new LagTestManager(_clockService, _systemService);
    }

    [Test]
    public async Task Test_StartLagTestAsync_ShouldReturnElapsedTime_WhenEchoIsHandled()
    {
        // Arrange
        var sleepTcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(sleepTcs.Task);

        var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMilliseconds(123);
        _clockService.CurrentUtcDateTimeOffset.Returns(start, end);

        var task = _manager.StartLagTestAsync("testroom");

        // Act
        _manager.HandleEcho("testroom");
        var result = await task;

        // Assert
        Assert.That(result.TotalMilliseconds, Is.EqualTo(123).Within(1));
    }

    [Test]
    public async Task Test_StartLagTestAsync_ShouldReturnTimeSpanMinValue_WhenTimeoutOccurs()
    {
        // Arrange
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.Delay(TimeSpan.FromMilliseconds(50)));

        // Act
        var result = await _manager.StartLagTestAsync("testroom");

        // Assert
        Assert.That(result, Is.EqualTo(TimeSpan.MinValue));
    }

    [Test]
    public async Task Test_StartLagTestAsync_ShouldReplaceExistingRequest_WhenCalledTwiceForSameRoom()
    {
        // Arrange
        var sleepTcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(sleepTcs.Task);

        var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMilliseconds(200);
        _clockService.CurrentUtcDateTimeOffset.Returns(start, start, end);

        var firstTask = _manager.StartLagTestAsync("testroom");
        var secondTask = _manager.StartLagTestAsync("testroom");

        // Act
        _manager.HandleEcho("testroom");
        var result = await secondTask;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TotalMilliseconds, Is.EqualTo(200).Within(1));
            Assert.That(firstTask.IsCanceled, Is.True);
        }
    }

    [Test]
    public void Test_HandleEcho_ShouldDoNothing_WhenNoLagTestIsPending()
    {
        // Act + Assert: no exception thrown
        Assert.DoesNotThrow(() => _manager.HandleEcho("nonexistent-room"));
    }

    [Test]
    public async Task Test_StartLagTestAsync_ShouldTrackDifferentRoomsIndependently()
    {
        // Arrange
        var sleepTcs = new TaskCompletionSource();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(sleepTcs.Task);

        var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _clockService.CurrentUtcDateTimeOffset.Returns(
            start,
            start,
            start.AddMilliseconds(100),
            start.AddMilliseconds(200));

        var taskA = _manager.StartLagTestAsync("room-a");
        var taskB = _manager.StartLagTestAsync("room-b");

        // Act
        _manager.HandleEcho("room-a");
        _manager.HandleEcho("room-b");

        var resultA = await taskA;
        var resultB = await taskB;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(resultA.TotalMilliseconds, Is.EqualTo(100).Within(1));
            Assert.That(resultB.TotalMilliseconds, Is.EqualTo(200).Within(1));
        }
    }
}
