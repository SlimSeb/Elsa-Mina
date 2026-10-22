namespace ElsaMina.Battles.Strategies;

public static class BattleDecisionStrategyExtensions
{
    /// <summary>
    /// The name staff type to pick the strategy: "damagecalc", "minipokev1-4m" for MiniPokeV1_4M.
    /// </summary>
    public static string GetName(this BattleDecisionStrategy strategy) =>
        strategy.ToString().ToLowerInvariant().Replace('_', '-');
}
