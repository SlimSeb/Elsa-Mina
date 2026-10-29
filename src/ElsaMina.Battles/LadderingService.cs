using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.System;
using ElsaMina.Logging;

namespace ElsaMina.Battles;

public class LadderingService : ILadderingService
{
    // Showdown refuses the 13th search, challenge or team validation of an IP within 3 minutes
    // (Monitor.countPrepBattle). We leave some of them to whatever else battles from this IP
    public const int MAX_SEARCHES_PER_WINDOW = 10;
    public static readonly TimeSpan SEARCH_WINDOW = TimeSpan.FromMinutes(3);

    // The server answers an accepted search with an updatesearch once the team is validated. A refused search
    // (rate limit, server restart, invalid team) or one dropped by the chat throttle never gets one
    public static readonly TimeSpan SEARCH_CONFIRMATION_TIMEOUT = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan FIRST_RETRY_DELAY = TimeSpan.FromSeconds(30);

    // The rate limit window lasts 3 minutes at most, waiting longer brings nothing
    public static readonly TimeSpan MAX_RETRY_DELAY = TimeSpan.FromMinutes(3);

    private readonly IBattleTeamSender _teamSender;
    private readonly IBattleTeamsService _battleTeamsService;
    private readonly IClockService _clockService;
    private readonly ISystemService _systemService;
    private readonly Lock _lock = new();
    private readonly Queue<DateTimeOffset> _recentSearchTimes = new();

    private string _format;
    private DateTimeOffset? _since;
    private bool _isSearchUnconfirmed;
    private int _failedSearchCount;
    private CancellationTokenSource _scheduledStep;
    private DateTimeOffset? _nextSearchAt;
    private bool _isSearching;
    private bool _isInGame;

    public LadderingService(IBattleTeamSender teamSender, IBattleTeamsService battleTeamsService,
        IClockService clockService, ISystemService systemService)
    {
        _teamSender = teamSender;
        _battleTeamsService = battleTeamsService;
        _clockService = clockService;
        _systemService = systemService;
    }

    public bool IsLaddering
    {
        get
        {
            lock (_lock)
            {
                return _format != null;
            }
        }
    }

    public string Format
    {
        get
        {
            lock (_lock)
            {
                return _format;
            }
        }
    }

    public LadderingStatus Status
    {
        get
        {
            lock (_lock)
            {
                return new LadderingStatus(_format, _since, _isSearching, _isInGame, _isSearchUnconfirmed,
                    _nextSearchAt, _failedSearchCount);
            }
        }
    }

    private bool IsSearchInProgress => _isSearchUnconfirmed || _scheduledStep != null;

    public void Start(string format)
    {
        lock (_lock)
        {
            if (_format != format)
            {
                _since = _clockService.CurrentUtcDateTimeOffset;
            }

            _format = format;
            _failedSearchCount = 0;
            RequestSearch();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _format = null;
            _since = null;
            _isSearchUnconfirmed = false;
            CancelScheduledStep();
        }
    }

    public void OnBattleEnded()
    {
        lock (_lock)
        {
            // The updatesearch sent when the battle ended may already have started the next search
            if (_format != null && !IsSearchInProgress)
            {
                RequestSearch();
            }
        }
    }

    public void OnSearchStateUpdated(bool isSearching, bool isInGame)
    {
        lock (_lock)
        {
            _isSearching = isSearching;
            _isInGame = isInGame;
            if (_format == null)
            {
                return;
            }

            if (isSearching || isInGame)
            {
                // The search went through, whether an opponent was found or not
                _isSearchUnconfirmed = false;
                _failedSearchCount = 0;
                CancelScheduledStep();
                return;
            }

            // Neither searching nor playing: a battle just ended, or the server dropped the search on a reconnection
            if (!IsSearchInProgress)
            {
                RequestSearch();
            }
        }
    }

    private void RequestSearch()
    {
        var delay = GetRateLimitDelay(_clockService.CurrentUtcDateTimeOffset);
        if (delay > TimeSpan.Zero)
        {
            Log.Information("Next ladder search in {Format} in {Delay}, to stay under Showdown's rate limit",
                _format, delay);
            ScheduleSearch(delay);
            return;
        }

        SendSearch();
    }

    private void ScheduleSearch(TimeSpan delay)
    {
        ScheduleStep(delay, SendSearch);
        _nextSearchAt = _clockService.CurrentUtcDateTimeOffset + delay;
    }

    private void SendSearch()
    {
        _nextSearchAt = null;
        _recentSearchTimes.Enqueue(_clockService.CurrentUtcDateTimeOffset);
        _isSearchUnconfirmed = true;
        // Challenges and team validations change the bot's team: set the format's own before each search
        _teamSender.SendWithTeam(_battleTeamsService.GetTeam(_format), $"/search {_format}");
        ScheduleStep(SEARCH_CONFIRMATION_TIMEOUT, HandleUnconfirmedSearch);
    }

    private void HandleUnconfirmedSearch()
    {
        _isSearchUnconfirmed = false;
        _failedSearchCount++;
        var retryDelay = GetRetryDelay(_failedSearchCount);
        var rateLimitDelay = GetRateLimitDelay(_clockService.CurrentUtcDateTimeOffset);
        var delay = retryDelay > rateLimitDelay ? retryDelay : rateLimitDelay;
        Log.Warning("Ladder search in {Format} was not accepted by the server (attempt {Attempt}), retrying in {Delay}",
            _format, _failedSearchCount, delay);
        ScheduleSearch(delay);
    }

    private TimeSpan GetRateLimitDelay(DateTimeOffset now)
    {
        while (_recentSearchTimes.Count > 0 && now - _recentSearchTimes.Peek() >= SEARCH_WINDOW)
        {
            _recentSearchTimes.Dequeue();
        }

        return _recentSearchTimes.Count < MAX_SEARCHES_PER_WINDOW
            ? TimeSpan.Zero
            : _recentSearchTimes.Peek() + SEARCH_WINDOW - now;
    }

    // 30s, 1min, 2min, then 3min
    private static TimeSpan GetRetryDelay(int failedSearchCount)
    {
        var delay = FIRST_RETRY_DELAY;
        for (var i = 1; i < failedSearchCount && delay < MAX_RETRY_DELAY; i++)
        {
            delay *= 2;
        }

        return delay < MAX_RETRY_DELAY ? delay : MAX_RETRY_DELAY;
    }

    private void ScheduleStep(TimeSpan delay, Action step)
    {
        CancelScheduledStep();
        var cancellationTokenSource = new CancellationTokenSource();
        _scheduledStep = cancellationTokenSource;
        _ = RunStepAsync(delay, step, cancellationTokenSource);
    }

    private void CancelScheduledStep()
    {
        _nextSearchAt = null;
        if (_scheduledStep == null)
        {
            return;
        }

        _scheduledStep.Cancel();
        _scheduledStep.Dispose();
        _scheduledStep = null;
    }

    private async Task RunStepAsync(TimeSpan delay, Action step, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await _systemService.SleepAsync(delay, cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_lock)
        {
            // Cancelled or replaced while waiting
            if (_scheduledStep != cancellationTokenSource || _format == null)
            {
                return;
            }

            _scheduledStep = null;
            cancellationTokenSource.Dispose();
            step();
        }
    }
}
