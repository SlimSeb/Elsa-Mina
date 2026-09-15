using ElsaMina.Core.Services.PlayTime;
using ElsaMina.Core.Services.Start;

namespace ElsaMina.Core.Services.Lifecycle;

public class BotLifecycleService : IBotLifecycleService
{
    private readonly IStartManager _startManager;
    private readonly IPlayTimeUpdateService _playTimeUpdateService;

    public BotLifecycleService(IStartManager startManager, IPlayTimeUpdateService playTimeUpdateService)
    {
        _startManager = startManager;
        _playTimeUpdateService = playTimeUpdateService;
    }

    public Task OnStartingAsync(CancellationToken cancellationToken = default)
    {
        return _startManager.LoadStaticDataAsync(cancellationToken);
    }

    public Task OnExitingAsync()
    {
        return _playTimeUpdateService.ProcessPendingPlayTimeUpdatesAsync();
    }
}
