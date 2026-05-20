using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Showdown.Ladder.EloHistory;

public class EloHistoryService : IEloHistoryService
{
    private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromHours(1);

    private readonly IEloProgressionManager _eloProgressionManager;
    private readonly IShowdownRanksProvider _showdownRanksProvider;
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly TimeSpan _pollInterval;

    private CancellationTokenSource _cts;
    private bool _disposed;

    public EloHistoryService(IEloProgressionManager eloProgressionManager,
        IShowdownRanksProvider showdownRanksProvider,
        IBotDbContextFactory dbContextFactory)
        : this(eloProgressionManager, showdownRanksProvider, dbContextFactory, POLL_INTERVAL)
    {
    }

    public EloHistoryService(IEloProgressionManager eloProgressionManager,
        IShowdownRanksProvider showdownRanksProvider,
        IBotDbContextFactory dbContextFactory,
        TimeSpan pollInterval)
    {
        _eloProgressionManager = eloProgressionManager;
        _showdownRanksProvider = showdownRanksProvider;
        _dbContextFactory = dbContextFactory;
        _pollInterval = pollInterval;
    }

    public void Start()
    {
        Log.Information("EloHistoryService starting with poll interval {PollInterval}", _pollInterval);
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_pollInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await PollOnceAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("EloHistoryService poll loop cancelled");
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var trackedUsers = _eloProgressionManager.GetAllTrackedUsers();
        Log.Information("EloHistoryService polling : {TrackedUserCount} tracked user(s)", trackedUsers.Count);

        if (trackedUsers.Count == 0)
        {
            return;
        }

        var recordedAt = DateTime.UtcNow;

        foreach (var trackedUser in trackedUsers)
        {
            try
            {
                await RecordSnapshotAsync(trackedUser, recordedAt, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to record ELO snapshot for user {UserId} in format {Format}",
                    trackedUser.UserId, trackedUser.Format);
            }
        }
    }

    private async Task RecordSnapshotAsync(EloTrackedUser trackedUser, DateTime recordedAt,
        CancellationToken cancellationToken)
    {
        Log.Information("Recording ELO snapshot for user {UserId} in format {Format}", trackedUser.UserId, trackedUser.Format);

        var rankingsEnumerable = await _showdownRanksProvider.GetRankingDataAsync(trackedUser.UserId, cancellationToken);
        if (rankingsEnumerable == null)
        {
            Log.Warning("No ranking data returned for user {UserId} : skipping snapshot", trackedUser.UserId);
            return;
        }

        var rankings = rankingsEnumerable.ToList();
        Log.Information("Fetched {RankingCount} ranking(s) for user {UserId}", rankings.Count, trackedUser.UserId);

        var entry = rankings.FirstOrDefault(r => r.FormatId == trackedUser.Format);
        if (entry == null)
        {
            Log.Warning("User {UserId} has no entry for format {Format} : skipping snapshot (available formats: {Formats})",
                trackedUser.UserId, trackedUser.Format, string.Join(", ", rankings.Select(r => r.FormatId)));
            return;
        }

        var snapshot = new LadderEloSnapshot
        {
            UserId = trackedUser.UserId,
            Format = trackedUser.Format,
            Elo = (int)Math.Round(entry.Elo, MidpointRounding.AwayFromZero),
            RecordedAt = recordedAt
        };

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.LadderEloSnapshots.AddAsync(snapshot, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        Log.Information("Recorded ELO snapshot for user {UserId} in format {Format}: {Elo}",
            trackedUser.UserId, trackedUser.Format, snapshot.Elo);
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
