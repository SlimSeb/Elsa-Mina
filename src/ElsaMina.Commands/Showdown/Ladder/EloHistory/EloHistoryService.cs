using ElsaMina.Commands.Showdown.BattleTracker;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Showdown.Ladder.EloHistory;

public class EloHistoryService : IEloHistoryService
{
    private const string LADDER_RESOURCE_URL = "https://pokemonshowdown.com/ladder/{0}.json";
    private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromHours(1);

    private readonly ILadderTrackerManager _ladderTrackerManager;
    private readonly IHttpService _httpService;
    private readonly IBotDbContextFactory _dbContextFactory;

    private CancellationTokenSource _cts;
    private bool _disposed;

    public EloHistoryService(ILadderTrackerManager ladderTrackerManager,
        IHttpService httpService,
        IBotDbContextFactory dbContextFactory)
    {
        _ladderTrackerManager = ladderTrackerManager;
        _httpService = httpService;
        _dbContextFactory = dbContextFactory;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(POLL_INTERVAL);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await PollOnceAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var trackings = _ladderTrackerManager.GetAllTrackings();
        if (trackings.Count == 0)
        {
            return;
        }

        var recordedAt = DateTime.UtcNow;

        foreach (var tracking in trackings)
        {
            try
            {
                await RecordSnapshotsForTrackingAsync(tracking, recordedAt, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to record ELO snapshots for format {Format} prefix {Prefix}",
                    tracking.Format, tracking.Prefix);
            }
        }
    }

    private async Task RecordSnapshotsForTrackingAsync(LadderTracking tracking, DateTime recordedAt,
        CancellationToken cancellationToken)
    {
        var response = await _httpService.GetAsync<LadderDto>(
            string.Format(LADDER_RESOURCE_URL, tracking.Format),
            cancellationToken: cancellationToken);

        if (response?.Data?.TopList == null)
        {
            return;
        }

        var snapshots = new List<LadderEloSnapshot>();

        foreach (var player in response.Data.TopList)
        {
            if (!string.IsNullOrWhiteSpace(tracking.Prefix) &&
                !player.Username.ToLower().Trim().StartsWith(tracking.Prefix))
            {
                continue;
            }

            var userId = string.IsNullOrWhiteSpace(player.UserId)
                ? player.Username?.ToLowerAlphaNum()
                : player.UserId.ToLowerAlphaNum();

            if (string.IsNullOrWhiteSpace(userId))
            {
                continue;
            }

            snapshots.Add(new LadderEloSnapshot
            {
                UserId = userId,
                Format = tracking.Format,
                Elo = (int)Math.Round(player.Elo, MidpointRounding.AwayFromZero),
                RecordedAt = recordedAt
            });
        }

        if (snapshots.Count == 0)
        {
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.LadderEloSnapshots.AddRangeAsync(snapshots, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
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

        _cts?.Cancel();
        _cts?.Dispose();
        _disposed = true;
    }
}
