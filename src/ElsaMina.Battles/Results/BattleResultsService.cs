using ElsaMina.Battles.Strategies;
using ElsaMina.Core.Services.Clock;

namespace ElsaMina.Battles.Results;

public class BattleResultsService : IBattleResultsService
{
    private readonly IBattleDecisionManager _battleDecisionManager;
    private readonly IClockService _clockService;
    private readonly Lock _lock = new();
    private readonly Dictionary<(string Format, BattleDecisionStrategy Strategy), BattleRecord> _records = [];
    private BattleResult _lastResult;

    public BattleResultsService(IBattleDecisionManager battleDecisionManager, IClockService clockService)
    {
        _battleDecisionManager = battleDecisionManager;
        _clockService = clockService;
    }

    public BattleResult LastResult
    {
        get
        {
            lock (_lock)
            {
                return _lastResult;
            }
        }
    }

    public void OnBattleStarted(BattleContext context)
    {
        context.Strategy ??= _battleDecisionManager.ActiveStrategy;
    }

    public void OnBattleEnded(BattleContext context, BattleOutcome outcome)
    {
        var strategy = context.Strategy ?? _battleDecisionManager.ActiveStrategy;
        var result = new BattleResult(context.RoomId, context.Format, strategy, outcome, context.Tracked.Turn,
            _clockService.CurrentUtcDateTimeOffset);
        lock (_lock)
        {
            var key = (result.Format, strategy);
            var record = _records.GetValueOrDefault(key) ?? new BattleRecord(result.Format, strategy, 0, 0, 0);
            _records[key] = record.Add(outcome);
            _lastResult = result;
        }
    }

    public IReadOnlyList<BattleRecord> GetRecords()
    {
        lock (_lock)
        {
            return _records.Values.OrderByDescending(record => record.Played).ToList();
        }
    }
}
