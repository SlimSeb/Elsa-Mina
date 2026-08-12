using ElsaMina.Core;
using ElsaMina.Logging;
using Lusamine.WebSocketClient.Events;

namespace ElsaMina.Console.Startup;

public sealed class BotHost
{
    // Handlers are I/O bound, and some await a response carried by a later message (see
    // PendingQueryRequestsManager), so this needs enough headroom that waiting handlers never
    // starve the pump of the slot needed to deliver what they are waiting for.
    private const int MAX_CONCURRENT_MESSAGES = 64;

    private readonly IBot _bot;
    private readonly IClient _client;
    private readonly ManualResetEvent _exitEvent = new(false);

    public BotHost(IBot bot, IClient client)
    {
        _bot = bot;
        _client = client;
    }

    public void Start()
    {
        _ = RunMessagePumpAsync();
        _client.Disconnected += OnClientDisconnected;
        _client.Connected += OnClientConnected;
    }

    public async Task RunAsync()
    {
        await _bot.StartAsync();
        _exitEvent.WaitOne();
    }

    public void Shutdown()
    {
        _bot.OnExit();
    }

    private async Task RunMessagePumpAsync()
    {
        try
        {
            await Parallel.ForEachAsync(
                _client.Messages,
                new ParallelOptions { MaxDegreeOfParallelism = MAX_CONCURRENT_MESSAGES },
                async (message, _) =>
                {
                    try
                    {
                        await _bot.HandleReceivedMessageAsync(message);
                    }
                    catch (Exception exception)
                    {
                        Log.Error(exception, "Error while handling message");
                    }
                });
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Message pump stopped unexpectedly");
        }
    }

    private void OnClientDisconnected(object sender, WebSocketDisconnectedEventArgs info)
    {
        Log.Warning(
            "Disconnected. Reason: {reason}, Status: {status}, Desc: {desc}, Exception: {ex}, Reconnecting: {reconnecting}",
            info.Reason,
            info.CloseStatus?.ToString() ?? string.Empty,
            info.CloseStatusDescription ?? string.Empty,
            info.Exception?.Message ?? string.Empty,
            info.WillReconnect
        );
        _bot.OnDisconnect();
    }

    private void OnClientConnected(object sender, WebSocketConnectedEventArgs info)
    {
        if (!info.IsReconnect)
        {
            return;
        }

        Log.Warning("Reconnected after {0} attempt(s), downtime : {1}", info.Attempt, info.Downtime);
        _bot.OnReconnect();
    }
}
