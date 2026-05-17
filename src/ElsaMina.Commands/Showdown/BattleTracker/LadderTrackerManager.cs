using System.Collections.Immutable;
using System.Globalization;
using ElsaMina.Core;
using ElsaMina.Core.Services.BattleTracker;
using ElsaMina.Core.Services.Formats;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Showdown.BattleTracker;

public class LadderTrackerManager : ILadderTrackerManager
{
    private const string UNKNOWN_LABEL = "unknown";
    private static readonly TimeSpan DEFAULT_POLL_INTERVAL = TimeSpan.FromSeconds(10);

    private readonly IActiveBattlesManager _activeBattlesManager;
    private readonly IBot _bot;
    private readonly IRoomsManager _roomsManager;
    private readonly IResourcesService _resourcesService;
    private readonly IFormatsManager _formatsManager;
    private readonly TimeSpan _pollInterval;
    private readonly Lock _lock = new();
    private readonly Dictionary<LadderTracking, TrackingState> _trackedBattles = [];

    private CancellationTokenSource _pollingCts;
    private bool _disposed;

    public LadderTrackerManager(IActiveBattlesManager activeBattlesManager, IBot bot,
        IRoomsManager roomsManager, IResourcesService resourcesService, IFormatsManager formatsManager)
        : this(activeBattlesManager, bot, roomsManager, resourcesService, formatsManager, DEFAULT_POLL_INTERVAL)
    {
    }

    public LadderTrackerManager(IActiveBattlesManager activeBattlesManager, IBot bot,
        IRoomsManager roomsManager, IResourcesService resourcesService, IFormatsManager formatsManager,
        TimeSpan pollInterval)
    {
        _activeBattlesManager = activeBattlesManager;
        _bot = bot;
        _roomsManager = roomsManager;
        _resourcesService = resourcesService;
        _formatsManager = formatsManager;
        _pollInterval = pollInterval;
    }

    public IReadOnlyCollection<LadderTracking> GetRoomTrackings(string roomId)
    {
        return _trackedBattles
            .Where(kvp => kvp.Value.SubscribedRoomIds.Contains(roomId))
            .Select(kvp => kvp.Key)
            .ToImmutableHashSet();
    }

    public IReadOnlyCollection<LadderTracking> GetAllTrackings()
    {
        lock (_lock)
        {
            return _trackedBattles.Keys.ToImmutableHashSet();
        }
    }

    public void StartTracking(string roomId, string format, string prefix)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(format))
        {
            return;
        }

        var trackingKey = new LadderTracking(format.ToLowerAlphaNum(), prefix ?? string.Empty);

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            var shouldStartPolling = _trackedBattles.Count == 0;

            if (!_trackedBattles.TryGetValue(trackingKey, out var trackingState))
            {
                trackingState = new TrackingState();
                _trackedBattles[trackingKey] = trackingState;
            }

            trackingState.SubscribedRoomIds.Add(roomId);

            if (shouldStartPolling)
            {
                StartPollingNoLock();
            }
        }
    }

    public void StopTracking(string roomId, string format, string prefix)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(format))
        {
            return;
        }

        var trackingKey = new LadderTracking(format.ToLowerAlphaNum(), prefix ?? string.Empty);

        CancellationTokenSource pollingCts;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (!_trackedBattles.TryGetValue(trackingKey, out var trackingState))
            {
                return;
            }

            if (!trackingState.SubscribedRoomIds.Remove(roomId))
            {
                return;
            }

            if (trackingState.SubscribedRoomIds.Count > 0)
            {
                return;
            }

            _trackedBattles.Remove(trackingKey);

            if (_trackedBattles.Count != 0)
            {
                return;
            }

            pollingCts = _pollingCts;
            _pollingCts = null;
        }

        pollingCts?.Cancel();
        pollingCts?.Dispose();
    }

    public void Dispose()
    {
        CancellationTokenSource pollingCts;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _trackedBattles.Clear();
            pollingCts = _pollingCts;
            _pollingCts = null;
        }

        pollingCts?.Cancel();
        pollingCts?.Dispose();
    }

    private void StartPollingNoLock()
    {
        _pollingCts = new CancellationTokenSource();
        _ = Task.Run(() => PollLoopAsync(_pollingCts.Token));
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
            // Normal: tracking stopped or manager disposed.
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        LadderTracking[] trackedKeys;

        lock (_lock)
        {
            trackedKeys = _trackedBattles.Keys.ToArray();
        }

        foreach (var trackingKey in trackedKeys)
        {
            IReadOnlyCollection<ActiveBattleDto> activeBattles;
            try
            {
                activeBattles = await _activeBattlesManager.GetActiveBattlesAsync(
                    trackingKey.Format,
                    prefixFilter: trackingKey.Prefix,
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while polling active battles for format {Format} and prefix {Prefix}",
                    trackingKey.Format,
                    trackingKey.Prefix);
                continue;
            }

            HandleBattleSnapshot(trackingKey, activeBattles);
        }
    }

    private void HandleBattleSnapshot(LadderTracking ladderTracking, IReadOnlyCollection<ActiveBattleDto> activeBattles)
    {
        var parsedBattles = activeBattles
            .Select(battle => (Battle: battle, BattleId: ParseBattleId(battle.RoomId)))
            .Where(result => result.BattleId.HasValue)
            .Select(result => (result.Battle, BattleId: result.BattleId.Value))
            .ToArray();

        ulong[] newBattleIds;
        string[] subscribedRoomIds;

        lock (_lock)
        {
            if (!_trackedBattles.TryGetValue(ladderTracking, out var state))
            {
                return;
            }

            if (!state.IsInitialized)
            {
                if (parsedBattles.Length > 0)
                {
                    state.LastBattleId = parsedBattles.Max(result => result.BattleId);
                }

                state.IsInitialized = true;
                return;
            }

            newBattleIds = parsedBattles
                .Where(result => result.BattleId > state.LastBattleId)
                .Select(result => result.BattleId)
                .Distinct()
                .Order()
                .ToArray();

            if (parsedBattles.Length > 0)
            {
                state.LastBattleId = Math.Max(state.LastBattleId, parsedBattles.Max(result => result.BattleId));
            }

            subscribedRoomIds = state.SubscribedRoomIds.ToArray();
        }

        foreach (var battleId in newBattleIds)
        {
            var battle = parsedBattles.First(result => result.BattleId == battleId).Battle;

            foreach (var roomId in subscribedRoomIds)
            {
                var room = _roomsManager.GetRoom(roomId);
                var message = BuildBattleStartMessage(room.Culture, ladderTracking.Format, battle, battleId);
                _bot.Say(roomId, $"/addhtmlbox {message}");
            }
        }
    }

    private static string FormatUsernameWithColor(string username)
    {
        return $"""<strong style="color: {username.ToColorHexCodeWithCustoms()}">{username}</strong>""";
    }

    private string BuildBattleStartMessage(CultureInfo cultureInfo, string format, ActiveBattleDto battle,
        ulong battleId)
    {
        var player1 = string.IsNullOrWhiteSpace(battle.Player1)
            ? UNKNOWN_LABEL
            : FormatUsernameWithColor(battle.Player1);
        var player2 = string.IsNullOrWhiteSpace(battle.Player2)
            ? UNKNOWN_LABEL
            : FormatUsernameWithColor(battle.Player2);
        var cleanFormat = _formatsManager.GetCleanFormat(format);
        var eloText = battle.MinElo?.ToString() ?? UNKNOWN_LABEL;
        var template = _resourcesService.GetString("battletracker_battle_started", cultureInfo);
        return $"""
                <a href="/battle-{format}-{battleId}" class="ilink" target="_blank" rel="noopener">
                    {string.Format(template, cleanFormat, player1, player2, eloText)}
                </a>
                """.RemoveNewlines();
    }

    private static ulong? ParseBattleId(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || !roomId.StartsWith("battle-"))
        {
            return null;
        }

        var lastDash = roomId.LastIndexOf('-');
        if (lastDash <= 0 || lastDash + 1 >= roomId.Length)
        {
            return null;
        }

        return ulong.TryParse(roomId[(lastDash + 1)..], out var battleId) ? battleId : null;
    }

    private sealed class TrackingState
    {
        public bool IsInitialized { get; set; }
        public ulong LastBattleId { get; set; }
        public ISet<string> SubscribedRoomIds { get; } = new HashSet<string>();
    }
}
