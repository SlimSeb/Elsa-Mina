using ElsaMina.Battles.Strategies.Llm;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies;

public class BattleDecisionManager : IBattleDecisionManager, IBattleDecisionService
{
    private readonly LlmBattleDecisionService _llmService;
    private readonly CalcBasedBattleDecisionService _calcService;
    private readonly TypeMatchupBattleDecisionService _typeMatchupService;
    private readonly RandomBattleDecisionService _randomService;

    public BattleDecisionManager(
        LlmBattleDecisionService llmService,
        CalcBasedBattleDecisionService calcService,
        TypeMatchupBattleDecisionService typeMatchupService,
        RandomBattleDecisionService randomService)
    {
        _llmService = llmService;
        _calcService = calcService;
        _typeMatchupService = typeMatchupService;
        _randomService = randomService;
    }

    public BattleDecisionStrategy ActiveStrategy { get; set; } = BattleDecisionStrategy.DamageCalc;

    public IReadOnlyList<BattleDecisionStrategy> AvailableStrategies =>
    [
        BattleDecisionStrategy.DamageCalc,
        BattleDecisionStrategy.Llm,
        BattleDecisionStrategy.TypeMatchup,
        BattleDecisionStrategy.Random
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
            default:
                strategy = ActiveStrategy;
                return false;
        }

        ActiveStrategy = strategy;
        Log.Information("Active battle decision strategy switched to: {Strategy}", strategy);
        return true;
    }

    public IBattleDecisionService GetCurrentService()
    {
        return ActiveStrategy switch
        {
            BattleDecisionStrategy.Llm => _llmService,
            BattleDecisionStrategy.DamageCalc => _calcService,
            BattleDecisionStrategy.TypeMatchup => _typeMatchupService,
            BattleDecisionStrategy.Random => _randomService,
            _ => _calcService
        };
    }

    public Task<BattleDecision> GetDecisionAsync(BattleContext context, CancellationToken cancellationToken = default)
    {
        var service = GetCurrentService();
        return service.GetDecisionAsync(context, cancellationToken);
    }
}
