using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Dispatch;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Telemetry;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Dispatch;

public class OutgoingMessageQueueTest
{
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(5);
    private static readonly DateTimeOffset START = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IClient _client;
    private IClockService _clockService;
    private ISystemService _systemService;
    private OutgoingMessageQueue _queue;
    private DateTimeOffset _now;
    private List<TimeSpan> _sleeps;

    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IClient>();
        _clockService = Substitute.For<IClockService>();
        _systemService = Substitute.For<ISystemService>();
        _now = START;
        _sleeps = [];
        _clockService.CurrentUtcDateTimeOffset.Returns(_ => _now);
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            var delay = callInfo.Arg<TimeSpan>();
            lock (_sleeps)
            {
                _sleeps.Add(delay);
            }

            _now += delay;
            return Task.CompletedTask;
        });
        _queue = new OutgoingMessageQueue(_client, _clockService, _systemService, Substitute.For<ITelemetryService>());
    }

    [Test]
    public void Test_Enqueue_ShouldSendImmediately_WhenCooldownHasPassed()
    {
        // Act
        _queue.Enqueue("room|hello");

        // Assert
        _client.Received(1).Send("room|hello");
        Assert.That(_queue.PendingCount, Is.Zero);
    }

    [Test]
    public async Task Test_Enqueue_ShouldSpaceMessagesByCooldown_WhenSentInBurst()
    {
        // Act
        _queue.Enqueue("room|1");
        _queue.Enqueue("room|2");
        _queue.Enqueue("room|3");
        await _queue.FlushAsync().WaitAsync(TIMEOUT);

        // Assert
        Received.InOrder(() =>
        {
            _client.Send("room|1");
            _client.Send("room|2");
            _client.Send("room|3");
        });
        Assert.That(_sleeps, Has.Count.EqualTo(2));
        Assert.That(_sleeps, Is.All.EqualTo(OutgoingMessageQueue.SEND_MESSAGE_COOLDOWN));
    }

    [Test]
    public async Task Test_Enqueue_ShouldKeepOrder_WhenManyThreadsSendAtOnce()
    {
        // Act
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() => _queue.Enqueue($"room|{i}"))));
        await _queue.FlushAsync().WaitAsync(TIMEOUT);

        // Assert
        _client.ReceivedWithAnyArgs(20).Send(default);
        Assert.That(_sleeps, Has.Count.EqualTo(19));
    }

    [Test]
    public void Test_Enqueue_ShouldDropDuplicate_WhenSameMessageIsSentWithinDuplicateWindow()
    {
        // Act
        _queue.Enqueue("room|hello");
        _now += TimeSpan.FromSeconds(1);
        _queue.Enqueue("room|hello");

        // Assert
        _client.Received(1).Send("room|hello");
    }

    [Test]
    public void Test_Enqueue_ShouldSendDuplicate_WhenDuplicateWindowHasPassed()
    {
        // Act
        _queue.Enqueue("room|hello");
        _now += OutgoingMessageQueue.SAME_MESSAGE_COOLDOWN;
        _queue.Enqueue("room|hello");

        // Assert
        _client.Received(2).Send("room|hello");
    }

    [Test]
    public async Task Test_Enqueue_ShouldKeepSending_WhenClientThrows()
    {
        // Arrange
        _client.When(client => client.Send("room|1")).Do(_ => throw new InvalidOperationException("boom"));

        // Act
        _queue.Enqueue("room|1");
        _queue.Enqueue("room|2");
        await _queue.FlushAsync().WaitAsync(TIMEOUT);

        // Assert
        _client.Received(1).Send("room|2");
    }
}
