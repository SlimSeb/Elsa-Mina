namespace ElsaMina.Core.Services.Dispatch;

public interface IOutgoingMessageQueue
{
    /// <summary>
    /// Sends <paramref name="message"/> right away when the send cooldown allows it, otherwise queues it.
    /// Queued messages go out in order, one cooldown apart, whichever thread queued them.
    /// The same message sent again within the duplicate window is dropped.
    /// </summary>
    void Enqueue(string message);

    /// <summary>
    /// The number of messages waiting for the cooldown.
    /// </summary>
    int PendingCount { get; }

    /// <summary>
    /// Completes once every queued message has been handed to the client.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
