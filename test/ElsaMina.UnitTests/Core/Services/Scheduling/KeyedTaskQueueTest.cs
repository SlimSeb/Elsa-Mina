using ElsaMina.Core.Services.Scheduling;

namespace ElsaMina.UnitTests.Core.Services.Scheduling;

public class KeyedTaskQueueTest
{
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(5);

    private KeyedTaskQueue _queue;

    [SetUp]
    public void SetUp()
    {
        _queue = new KeyedTaskQueue();
    }

    [Test]
    public async Task Test_EnqueueAsync_ShouldRunWorkInOrder_WhenKeysAreTheSame()
    {
        // Arrange
        var executionOrder = new List<int>();
        var releaseFirst = new TaskCompletionSource();

        // Act
        var first = _queue.EnqueueAsync("room", async () =>
        {
            await releaseFirst.Task;
            executionOrder.Add(1);
        });
        var second = _queue.EnqueueAsync("room", () =>
        {
            executionOrder.Add(2);
            return Task.CompletedTask;
        });
        await Task.Delay(50);
        var secondRanBeforeFirstCompleted = second.IsCompleted;
        releaseFirst.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TIMEOUT);

        // Assert
        Assert.That(secondRanBeforeFirstCompleted, Is.False);
        Assert.That(executionOrder, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task Test_EnqueueAsync_ShouldRunConcurrently_WhenKeysAreDifferent()
    {
        // Arrange
        var releaseFirst = new TaskCompletionSource();
        var first = _queue.EnqueueAsync("room1", () => releaseFirst.Task);

        // Act
        await _queue.EnqueueAsync("room2", () => Task.CompletedTask).WaitAsync(TIMEOUT);

        // Assert
        Assert.That(first.IsCompleted, Is.False);
        releaseFirst.SetResult();
        await first.WaitAsync(TIMEOUT);
    }

    [Test]
    public async Task Test_EnqueueAsync_ShouldRunNextWork_WhenPreviousWorkFails()
    {
        // Arrange
        var failing = _queue.EnqueueAsync("room", () => throw new InvalidOperationException("boom"));
        var secondRan = false;

        // Act
        var second = _queue.EnqueueAsync("room", () =>
        {
            secondRan = true;
            return Task.CompletedTask;
        });
        await second.WaitAsync(TIMEOUT);

        // Assert
        Assert.That(secondRan, Is.True);
        Assert.ThrowsAsync<InvalidOperationException>(() => failing);
    }

    [Test]
    public async Task Test_EnqueueAsync_ShouldNotRunWorkSynchronously()
    {
        // Arrange
        var callerThreadId = Environment.CurrentManagedThreadId;
        var ranSynchronously = false;

        // Act
        var task = _queue.EnqueueAsync("room", () =>
        {
            ranSynchronously = Environment.CurrentManagedThreadId == callerThreadId && !Thread.CurrentThread.IsThreadPoolThread;
            return Task.CompletedTask;
        });
        var completedSynchronously = task.IsCompleted;
        await task.WaitAsync(TIMEOUT);

        // Assert
        Assert.That(completedSynchronously, Is.False);
        Assert.That(ranSynchronously, Is.False);
    }

    [Test]
    public async Task Test_ActiveKeyCount_ShouldDropKey_WhenItsWorkHasCompleted()
    {
        // Arrange
        await _queue.EnqueueAsync("room", () => Task.CompletedTask).WaitAsync(TIMEOUT);

        // Act
        await Task.Delay(50);

        // Assert
        Assert.That(_queue.ActiveKeyCount, Is.Zero);
    }
}
