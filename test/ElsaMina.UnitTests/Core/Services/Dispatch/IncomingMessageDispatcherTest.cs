using System.Collections.Concurrent;
using ElsaMina.Core;
using ElsaMina.Core.Services.Dispatch;
using ElsaMina.Core.Services.Scheduling;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Dispatch;

public class IncomingMessageDispatcherTest
{
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(5);

    private IBot _bot;
    private IncomingMessageDispatcher _dispatcher;

    [SetUp]
    public void SetUp()
    {
        _bot = Substitute.For<IBot>();
        _dispatcher = new IncomingMessageDispatcher(_bot, new KeyedTaskQueue());
    }

    [Test]
    public async Task Test_DispatchAsync_ShouldHandleFramesOfSameRoomInOrder_WhenFirstFrameIsSlow()
    {
        // Arrange
        var handled = new ConcurrentQueue<string>();
        var releaseFirst = new TaskCompletionSource();
        _bot.HandleReceivedMessageAsync(Arg.Any<string>()).Returns(async callInfo =>
        {
            var frame = callInfo.Arg<string>();
            if (frame.EndsWith("first"))
            {
                await releaseFirst.Task;
            }

            handled.Enqueue(frame);
        });

        // Act
        await _dispatcher.DispatchAsync(">room\n|c:|1|%A|first");
        await _dispatcher.DispatchAsync(">room\n|c:|1|%A|second");
        await Task.Delay(50);
        var handledBeforeRelease = handled.Count;
        releaseFirst.SetResult();
        await _dispatcher.WhenIdleAsync().WaitAsync(TIMEOUT);

        // Assert
        Assert.That(handledBeforeRelease, Is.Zero);
        Assert.That(handled, Is.EqualTo(new[] { ">room\n|c:|1|%A|first", ">room\n|c:|1|%A|second" }));
    }

    [Test]
    public async Task Test_DispatchAsync_ShouldHandleOtherRooms_WhenOneRoomIsBlocked()
    {
        // Arrange
        var blockRoom = new TaskCompletionSource();
        var otherRoomHandled = new TaskCompletionSource();
        _bot.HandleReceivedMessageAsync(Arg.Any<string>()).Returns(callInfo =>
        {
            var frame = callInfo.Arg<string>();
            if (frame.StartsWith(">blocked"))
            {
                return blockRoom.Task;
            }

            otherRoomHandled.TrySetResult();
            return Task.CompletedTask;
        });

        // Act
        await _dispatcher.DispatchAsync(">blocked\n|c:|1|%A|hi");
        await _dispatcher.DispatchAsync(">other\n|c:|1|%A|hi");

        // Assert
        await otherRoomHandled.Task.WaitAsync(TIMEOUT);
        Assert.That(blockRoom.Task.IsCompleted, Is.False);
        blockRoom.SetResult();
        await _dispatcher.WhenIdleAsync().WaitAsync(TIMEOUT);
    }

    [Test]
    public async Task Test_DispatchAsync_ShouldKeepDispatching_WhenAFrameFails()
    {
        // Arrange
        var handled = new ConcurrentQueue<string>();
        _bot.HandleReceivedMessageAsync(Arg.Any<string>()).Returns(callInfo =>
        {
            var frame = callInfo.Arg<string>();
            if (frame.EndsWith("boom"))
            {
                throw new InvalidOperationException("boom");
            }

            handled.Enqueue(frame);
            return Task.CompletedTask;
        });

        // Act
        await _dispatcher.DispatchAsync(">room\n|c:|1|%A|boom");
        await _dispatcher.DispatchAsync(">room\n|c:|1|%A|ok");
        await _dispatcher.WhenIdleAsync().WaitAsync(TIMEOUT);

        // Assert
        Assert.That(handled, Is.EqualTo(new[] { ">room\n|c:|1|%A|ok" }));
    }
}
