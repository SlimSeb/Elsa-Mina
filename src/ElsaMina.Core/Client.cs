using System.Net.WebSockets;
using ElsaMina.Core.Services.Config;
using ElsaMina.Logging;
using Lusamine.WebSocketClient;
using Lusamine.WebSocketClient.Events;
using Lusamine.WebSocketClient.Logging;

namespace ElsaMina.Core;

public class Client : IClient
{
    private static readonly TimeSpan RECONNECT_DELAY = TimeSpan.FromSeconds(30);
    private readonly IConfiguration _configuration;
    private readonly WebSocketClient _webSocketClient;
    private readonly IAsyncEnumerable<string> _messages;
    private bool _disposed;

    public Client(IConfiguration configuration)
    {
        _configuration = configuration;

        _webSocketClient = WebSocketClientBuilder.Create(Uri)
            .WithFixedReconnect(RECONNECT_DELAY)
            .WithLogger(LogClientMessage, WebSocketLogLevel.Debug)
            .Build();

        // Reading the Messages property is what allocates the inbound buffer. Doing it here, before the
        // connection starts, means nothing that arrives before the first enumeration can be dropped.
        _messages = ReadTextMessages(_webSocketClient.Messages);
    }

    private Uri Uri => new($"wss://{_configuration.Host}:{_configuration.Port}/showdown/websocket");

    public IAsyncEnumerable<string> Messages => _messages;

    public event EventHandler<WebSocketDisconnectedEventArgs> Disconnected
    {
        add => _webSocketClient.Disconnected += value;
        remove => _webSocketClient.Disconnected -= value;
    }

    public event EventHandler<WebSocketConnectedEventArgs> Connected
    {
        add => _webSocketClient.Connected += value;
        remove => _webSocketClient.Connected -= value;
    }

    public bool IsConnected => _webSocketClient.IsConnected;

    public async Task Connect()
    {
        Log.Information("Connecting to : {0}", _webSocketClient.Uri);
        await _webSocketClient.StartAsync();
    }

    public async Task Close()
    {
        Log.Information("Closing connection");
        await _webSocketClient.StopAsync(WebSocketCloseStatus.NormalClosure);
    }

    public void Send(string message)
    {
        if (!_webSocketClient.TryPost(message))
        {
            Log.Warning("Could not queue message for sending : {0}", message);
        }
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _webSocketClient.SendAsync(message, cancellationToken);
    }

    private static async IAsyncEnumerable<string> ReadTextMessages(IAsyncEnumerable<WebSocketMessage> messages)
    {
        await foreach (var message in messages)
        {
            yield return message.Text;
        }
    }

    private static void LogClientMessage(WebSocketLogLevel level, string message, Exception exception)
    {
        switch (level)
        {
            case WebSocketLogLevel.Error:
                Log.Error(exception, "[WebSocket] {0}", message);
                break;
            case WebSocketLogLevel.Warning:
                Log.Warning(exception, "[WebSocket] {0}", message);
                break;
            case WebSocketLogLevel.Information:
                Log.Information("[WebSocket] {0}", message);
                break;
            default:
                Log.Debug("[WebSocket] {0}", message);
                break;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _webSocketClient.Dispose();
        }

        _disposed = true;
    }

    ~Client()
    {
        Dispose(false);
    }
}
