using ElsaMina.Battles.Strategies.Gladion;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies;

public class BattleDecisionManager : IBattleDecisionManager, IBattleDecisionService
{
    private readonly CalcBasedBattleDecisionService _calcService;
    private readonly Dictionary<BattleDecisionStrategy, GladionBattleDecisionService> _gladionServices;

    public BattleDecisionManager(
        CalcBasedBattleDecisionService calcService,
        IEnumerable<GladionBattleDecisionService> gladionServices)
    {
        _calcService = calcService;
        // One per Gladion model registered in BattlesModule
        _gladionServices = gladionServices.ToDictionary(service => service.Strategy);
    }

    public BattleDecisionStrategy ActiveStrategy { get; set; } = BattleDecisionStrategy.GladionV1_22M_Search;

    /// <summary>
    /// The damage calc, every Gladion model, and their search variants when Showdown's simulator is configured.
    /// </summary>
    public IReadOnlyList<BattleDecisionStrategy> AvailableStrategies =>
    [
        BattleDecisionStrategy.DamageCalc,
        .._gladionServices.Where(pair => IsPlayable(pair.Value)).Select(pair => pair.Key).Order()
    ];

    public bool TrySetStrategy(string strategyName, out BattleDecisionStrategy strategy)
    {
        if (string.IsNullOrWhiteSpace(strategyName))
        {
            strategy = ActiveStrategy;
            return false;
        }

        var normalized = strategyName.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "calc" or "damagecalc" or "damage-calc" or "minimax" or "simulation" or "calcbased":
                strategy = BattleDecisionStrategy.DamageCalc;
                break;
            case "gladion" when _gladionServices.Keys.Any(candidate => !candidate.IsSearch()):
                // The latest model
                strategy = _gladionServices.Keys.Where(candidate => !candidate.IsSearch()).Max();
                break;
            default:
                if (!TryFindGladionStrategy(normalized, out strategy))
                {
                    strategy = ActiveStrategy;
                    return false;
                }

                break;
        }

        ActiveStrategy = strategy;
        Log.Information("Active battle decision strategy switched to: {Strategy}", strategy);
        return true;
    }

    /// <summary>
    /// A Gladion model by its strategy name ("gladionv1-22m").
    /// </summary>
    private bool TryFindGladionStrategy(string name, out BattleDecisionStrategy strategy)
    {
        var matches = _gladionServices
            .Where(pair => pair.Key.GetName() == name && IsPlayable(pair.Value))
            .Select(pair => pair.Key)
            .ToList();
        strategy = matches.FirstOrDefault();
        return matches.Count > 0;
    }

    /// <summary>
    /// A search variant only plays when it can search: without the simulator it would be its model alone.
    /// </summary>
    private static bool IsPlayable(GladionBattleDecisionService service) =>
        !service.Strategy.IsSearch() || service.CanSearch;

    /// <summary>
    /// The service playing the active strategy: the damage calc one when its Gladion model is not registered.
    /// </summary>
    public IBattleDecisionService GetCurrentService() =>
        _gladionServices.TryGetValue(ActiveStrategy, out var gladionService) ? gladionService : _calcService;

    public Task<BattleDecision> GetDecisionAsync(BattleContext context, CancellationToken cancellationToken = default)
    {
        var service = GetCurrentService();
        return service.GetDecisionAsync(context, cancellationToken);
    }
}
