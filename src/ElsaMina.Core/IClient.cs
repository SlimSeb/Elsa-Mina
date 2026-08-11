using Lusamine.WebSocketClient.Events;

namespace ElsaMina.Core;

public interface IClient : IDisposable
{
    Task Connect();
    void Send(string message);
    Task SendAsync(string message, CancellationToken cancellationToken);
    Task Close();
    IAsyncEnumerable<string> Messages { get; }
    event EventHandler<WebSocketDisconnectedEventArgs> Disconnected;
    event EventHandler<WebSocketConnectedEventArgs> Connected;
    bool IsConnected { get; }
}
