using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Dispatch;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Telemetry;
using ElsaMina.Logging;

namespace ElsaMina.Core;

public class Bot : IBot
{
    private const int MESSAGE_LENGTH_LIMIT = 125_000;

    private readonly IClient _client;
    private readonly IClockService _clockService;
    private readonly IRoomsManager _roomsManager;
    private readonly IHandlerManager _handlerManager;
    private readonly IOutgoingMessageQueue _outgoingMessageQueue;
    private readonly IBotLifecycleService _botLifecycleService;
    private readonly ITelemetryService _telemetryService;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private DateTimeOffset _connectionTime;
    private bool _disposed;

    public Bot(IClient client,
        IClockService clockService,
        IRoomsManager roomsManager,
        IHandlerManager handlerManager,
        IOutgoingMessageQueue outgoingMessageQueue,
        IBotLifecycleService botLifecycleService,
        ITelemetryService telemetryService)
    {
        _client = client;
        _clockService = clockService;
        _roomsManager = roomsManager;
        _handlerManager = handlerManager;
        _outgoingMessageQueue = outgoingMessageQueue;
        _botLifecycleService = botLifecycleService;
        _telemetryService = telemetryService;
    }

    public async Task StartAsync()
    {
        _handlerManager.Initialize();
        await _botLifecycleService.OnStartingAsync(_cancellationTokenSource.Token);
        await _client.Connect();
        _connectionTime = _clockService.CurrentUtcDateTimeOffset;
    }

    public void OnReconnect()
    {
        _telemetryService.RecordWebSocketReconnection();
        _connectionTime = _clockService.CurrentUtcDateTimeOffset;
    }

    public void OnDisconnect()
    {
        _roomsManager.Clear();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Log.Information("Exiting bot...");
        try
        {
            await _botLifecycleService.OnExitingAsync(cancellationToken);
            await _outgoingMessageQueue.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Shutdown work did not complete in time");
        }
    }

    public TimeSpan UpTime => _clockService.CurrentUtcDateTimeOffset - _connectionTime;

    public async Task HandleReceivedMessageAsync(string message)
    {
        var lines = message.Split("\n").Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        if (lines.Length == 0)
        {
            return;
        }

        var roomId = ShowdownFrame.GetRoomId(lines[0]);
        var firstBodyLine = lines[0][0] == '>' ? 1 : 0;

        if (lines.Length > firstBodyLine + 1 && lines[firstBodyLine].StartsWith("|init|chat"))
        {
            await _roomsManager.InitializeRoomAsync(roomId, lines, _cancellationTokenSource.Token);
            return;
        }

        foreach (var line in lines)
        {
            await ReadLine(roomId, line);
        }
    }

    private async Task ReadLine(string roomId, string line)
    {
        var parts = line.Split("|");
        if (parts.Length < 2)
        {
            return;
        }

        Log.Debug("[Received] ({0}) {1}", roomId, line);

        _telemetryService.RecordMessageReceived(roomId, parts[1]);

        await _handlerManager.HandleMessageAsync(parts, roomId, _cancellationTokenSource.Token);
    }

    public void Send(string message)
    {
        if (message.Length > MESSAGE_LENGTH_LIMIT)
        {
            throw new ArgumentException("Message length limit reached", nameof(message));
        }

        _outgoingMessageQueue.Enqueue(message);
    }

    public void Say(string roomId, string message)
    {
        Send($"{roomId}|{message}");
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
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _client.Dispose();
        }

        _disposed = true;
    }
}
