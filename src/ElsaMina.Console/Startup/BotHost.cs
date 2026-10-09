using ElsaMina.Core;
using ElsaMina.Core.Services.Dispatch;
using ElsaMina.Logging;
using Lusamine.WebSocketClient.Events;

namespace ElsaMina.Console.Startup;

public sealed class BotHost
{
    // Le script de déploiement envoie un SIGTERM puis attend 10s avant de kill le process
    private static readonly TimeSpan SHUTDOWN_TIMEOUT = TimeSpan.FromSeconds(8);

    private readonly IBot _bot;
    private readonly IClient _client;
    private readonly IIncomingMessageDispatcher _dispatcher;
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _shutdownLock = new();
    private Task _shutdownTask;

    public BotHost(IBot bot, IClient client, IIncomingMessageDispatcher dispatcher)
    {
        _bot = bot;
        _client = client;
        _dispatcher = dispatcher;
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
        await _stopped.Task;
    }

    /// <summary>
    /// Flush le taf en attente et laisse <see cref="RunAsync"/> se terminer. On peut l'appeler plusieurs fois sans pb
    /// </summary>
    public Task ShutdownAsync()
    {
        lock (_shutdownLock)
        {
            return _shutdownTask ??= RunShutdownAsync();
        }
    }

    private async Task RunShutdownAsync()
    {
        using var timeout = new CancellationTokenSource(SHUTDOWN_TIMEOUT);
        try
        {
            await _bot.StopAsync(timeout.Token);
        }
        finally
        {
            _stopped.TrySetResult();
        }
    }

    private async Task RunMessagePumpAsync()
    {
        try
        {
            // La lecture reste séquentielle pour que le dispatcher voie les frames dans l'ordre d'arrivée ; ensuite il traite
            // les frames de chaque room dans l'ordre, et les rooms différentes en parallèle
            await foreach (var message in _client.Messages)
            {
                await _dispatcher.DispatchAsync(message);
            }
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
