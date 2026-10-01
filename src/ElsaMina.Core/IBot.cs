namespace ElsaMina.Core;

public interface IBot : IDisposable
{
    /// <summary>
    /// Handles one frame received from the server. Callers that receive frames concurrently must
    /// go through <see cref="Services.Dispatch.IIncomingMessageDispatcher"/>, which keeps each room's frames in order.
    /// </summary>
    Task HandleReceivedMessageAsync(string message);
    void Send(string message);
    void Say(string roomId, string message);
    Task StartAsync();
    void OnReconnect();
    void OnDisconnect();

    /// <summary>
    /// Runs the shutdown work (pending saves, queued messages) and waits for it, until <paramref name="cancellationToken"/> fires.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
    TimeSpan UpTime { get; }
}
