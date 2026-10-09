using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Telemetry;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Dispatch;

public class OutgoingMessageQueue : IOutgoingMessageQueue
{
    public static readonly TimeSpan SEND_MESSAGE_COOLDOWN = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan SAME_MESSAGE_COOLDOWN = TimeSpan.FromSeconds(3);

    private readonly IClient _client;
    private readonly IClockService _clockService;
    private readonly ISystemService _systemService;
    private readonly ITelemetryService _telemetryService;

    private readonly Lock _lock = new();
    private readonly Queue<string> _pending = new();
    private Task _drainTask = Task.CompletedTask;
    private bool _isDraining;
    private DateTimeOffset _nextSendTime = DateTimeOffset.MinValue;
    private string _lastMessage;
    private DateTimeOffset _lastMessageTime = DateTimeOffset.MinValue;

    public OutgoingMessageQueue(IClient client, IClockService clockService, ISystemService systemService,
        ITelemetryService telemetryService)
    {
        _client = client;
        _clockService = clockService;
        _systemService = systemService;
        _telemetryService = telemetryService;
    }

    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _pending.Count;
            }
        }
    }

    public void Enqueue(string message)
    {
        lock (_lock)
        {
            var now = _clockService.CurrentUtcDateTimeOffset;
            if (_lastMessage == message && now - _lastMessageTime < SAME_MESSAGE_COOLDOWN)
            {
                return;
            }

            _lastMessage = message;
            _lastMessageTime = now;

            if (!_isDraining && now >= _nextSendTime)
            {
                SendNow(message, now);
                return;
            }

            _pending.Enqueue(message);
            if (!_isDraining)
            {
                _isDraining = true;
                _drainTask = DrainAsync();
            }
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        Task drainTask;
        lock (_lock)
        {
            drainTask = _drainTask;
        }

        return drainTask.WaitAsync(cancellationToken);
    }

    private async Task DrainAsync()
    {
        // On sort du lock de l'appelant avant d'attendre
        await Task.Yield();
        while (true)
        {
            TimeSpan delay;
            lock (_lock)
            {
                if (_pending.Count == 0)
                {
                    _isDraining = false;
                    return;
                }

                delay = _nextSendTime - _clockService.CurrentUtcDateTimeOffset;
            }

            if (delay > TimeSpan.Zero)
            {
                await _systemService.SleepAsync(delay);
            }

            lock (_lock)
            {
                // On fait confiance à l'attente plutôt que de relire l'horloge, sinon une horloge pas précise peut bloquer la file
                var now = _clockService.CurrentUtcDateTimeOffset;
                SendNow(_pending.Dequeue(), now > _nextSendTime ? now : _nextSendTime);
            }
        }
    }

    // A appeler en tenant _lock !!
    private void SendNow(string message, DateTimeOffset now)
    {
        Log.Debug("[Sending] {0}", message);
        try
        {
            _telemetryService.RecordMessageSent();
            _client.Send(message);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not send message : {0}", message);
        }

        _nextSendTime = now + SEND_MESSAGE_COOLDOWN;
    }
}
