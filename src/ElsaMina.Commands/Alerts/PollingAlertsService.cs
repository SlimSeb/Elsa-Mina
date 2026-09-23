using ElsaMina.Core;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Alerts;

public abstract class PollingAlertsService : IPollingAlertsService
{
    private readonly IRoomsManager _roomsManager;
    private readonly ITemplatesManager _templatesManager;
    private readonly IBot _bot;
    private readonly TimeSpan _pollInterval;

    private CancellationTokenSource _cancellationTokenSource;
    private bool _disposed;

    protected PollingAlertsService(IRoomsManager roomsManager, ITemplatesManager templatesManager, IBot bot,
        TimeSpan pollInterval)
    {
        _roomsManager = roomsManager;
        _templatesManager = templatesManager;
        _bot = bot;
        _pollInterval = pollInterval;
    }

    protected TimeSpan PollInterval => _pollInterval;
    protected abstract bool IsConfigured { get; }

    public void Start()
    {
        var serviceName = GetType().Name;
        if (!IsConfigured)
        {
            Log.Warning("{Service} is not configured : alerts are disabled", serviceName);
            return;
        }

        Log.Information("{Service} starting with poll interval {PollInterval}", serviceName, _pollInterval);
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;
        _ = Task.Run(() => PollLoopAsync(cancellationToken), cancellationToken);
    }

    public abstract Task PollOnceAsync(CancellationToken cancellationToken = default);

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_pollInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await PollOnceAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "{Service} failed to poll", GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("{Service} poll loop cancelled", GetType().Name);
        }
    }

    protected async Task AnnounceAsync<TViewModel>(IEnumerable<string> roomIds, string templateKey,
        TViewModel viewModel) where TViewModel : LocalizableViewModel
    {
        foreach (var roomId in roomIds.Distinct())
        {
            var room = _roomsManager.GetRoom(roomId);
            if (room == null)
            {
                continue;
            }

            try
            {
                viewModel.Culture = room.Culture;
                var template = await _templatesManager.GetTemplateAsync(templateKey, viewModel);
                _bot.Say(roomId, $"/addhtmlbox {template.RemoveNewlines()}");
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to announce alert {Template} in room {RoomId}", templateKey, roomId);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _disposed = true;
    }
}
