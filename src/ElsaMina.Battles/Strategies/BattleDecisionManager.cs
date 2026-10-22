using ElsaMina.Battles.Strategies.Llm;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies;

public class BattleDecisionManager : IBattleDecisionManager, IBattleDecisionService
{
    private readonly LlmBattleDecisionService _llmService;
    private readonly CalcBasedBattleDecisionService _calcService;
    private readonly TypeMatchupBattleDecisionService _typeMatchupService;
    private readonly RandomBattleDecisionService _randomService;
    private readonly Dictionary<BattleDecisionStrategy, MiniPokeBattleDecisionService> _miniPokeServices;

    public BattleDecisionManager(
        LlmBattleDecisionService llmService,
        CalcBasedBattleDecisionService calcService,
        TypeMatchupBattleDecisionService typeMatchupService,
        RandomBattleDecisionService randomService,
        IEnumerable<MiniPokeBattleDecisionService> miniPokeServices)
    {
        _llmService = llmService;
        _calcService = calcService;
        _typeMatchupService = typeMatchupService;
        _randomService = randomService;
        // One per minipoke model registered in BattlesModule
        _miniPokeServices = miniPokeServices.ToDictionary(service => service.Strategy);
    }

    public BattleDecisionStrategy ActiveStrategy { get; set; } = BattleDecisionStrategy.DamageCalc;

    public IReadOnlyList<BattleDecisionStrategy> AvailableStrategies =>
    [
        BattleDecisionStrategy.DamageCalc,
        BattleDecisionStrategy.Llm,
        BattleDecisionStrategy.TypeMatchup,
        BattleDecisionStrategy.Random,
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
            case "llm" or "ai" or "language-model" or "languagemodel":
                strategy = BattleDecisionStrategy.Llm;
                break;
            case "calc" or "damagecalc" or "damage-calc" or "minimax" or "simulation" or "calcbased":
                strategy = BattleDecisionStrategy.DamageCalc;
                break;
            case "type" or "typematchup" or "type-matchup" or "matchup":
                strategy = BattleDecisionStrategy.TypeMatchup;
                break;
            case "random" or "rand":
                strategy = BattleDecisionStrategy.Random;
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

    public IBattleDecisionService GetCurrentService()
    {
        return ActiveStrategy switch
        {
            BattleDecisionStrategy.Llm => _llmService,
            BattleDecisionStrategy.DamageCalc => _calcService,
            BattleDecisionStrategy.TypeMatchup => _typeMatchupService,
            BattleDecisionStrategy.Random => _randomService,
            _ => _miniPokeServices.TryGetValue(ActiveStrategy, out var miniPokeService) ? miniPokeService : _calcService
        };
    }

    public Task<BattleDecision> GetDecisionAsync(BattleContext context, CancellationToken cancellationToken = default)
    {
        var service = GetCurrentService();
        return service.GetDecisionAsync(context, cancellationToken);
    }
}
