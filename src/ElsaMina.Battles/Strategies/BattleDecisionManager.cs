using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies;

public class BattleDecisionManager : IBattleDecisionManager, IBattleDecisionService
{
    private readonly CalcBasedBattleDecisionService _calcService;
    private readonly Dictionary<BattleDecisionStrategy, MiniPokeBattleDecisionService> _miniPokeServices;

    public BattleDecisionManager(
        CalcBasedBattleDecisionService calcService,
        IEnumerable<MiniPokeBattleDecisionService> miniPokeServices)
    {
        _calcService = calcService;
        // One per minipoke model registered in BattlesModule
        _miniPokeServices = miniPokeServices.ToDictionary(service => service.Strategy);
    }

    public BattleDecisionStrategy ActiveStrategy { get; set; } = BattleDecisionStrategy.MiniPokeV2_4M;

    public IReadOnlyList<BattleDecisionStrategy> AvailableStrategies =>
    [
        BattleDecisionStrategy.DamageCalc,
        .._miniPokeServices.Keys.Order()
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
            case "minipoke" when _miniPokeServices.Count > 0:
                // The latest model
                strategy = _miniPokeServices.Keys.Max();
                break;
            default:
                if (!TryFindMiniPokeStrategy(normalized, out strategy))
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
    /// A minipoke model by its strategy name ("minipokev1-4m").
    /// </summary>
    private bool TryFindMiniPokeStrategy(string name, out BattleDecisionStrategy strategy)
    {
        var matches = _miniPokeServices.Keys
            .Where(candidate => candidate.GetName() == name)
            .ToList();
        strategy = matches.FirstOrDefault();
        return matches.Count > 0;
    }

    /// <summary>
    /// The service playing the active strategy: the damage calc one when its minipoke model is not registered.
    /// </summary>
    public IBattleDecisionService GetCurrentService() =>
        _miniPokeServices.TryGetValue(ActiveStrategy, out var miniPokeService) ? miniPokeService : _calcService;

    public Task<BattleDecision> GetDecisionAsync(BattleContext context, CancellationToken cancellationToken = default)
    {
        var service = GetCurrentService();
        return service.GetDecisionAsync(context, cancellationToken);
    }
}
