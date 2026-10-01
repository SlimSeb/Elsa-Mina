namespace ElsaMina.Core.Services.Dispatch;

public interface IIncomingMessageDispatcher
{
    /// <summary>
    /// Schedules a frame received from the server. Frames of the same room are handled one after the
    /// other, in the order they were received; frames of different rooms are handled concurrently.
    /// Returns once the frame is scheduled, not once it is handled.
    /// </summary>
    Task DispatchAsync(string frame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes once every frame scheduled so far has been handled.
    /// </summary>
    Task WhenIdleAsync(CancellationToken cancellationToken = default);
}
