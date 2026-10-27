namespace ElsaMina.Battles.Strategies;

public static class BattleDecisionStrategyExtensions
{
    /// <summary>
    /// The name staff type to pick the strategy: "damagecalc", "minipokev1-4m" for MiniPokeV1_4M, "minipokev2.1-1m"
    /// for MiniPokeV2_1_1M (the last underscore comes before the size, the others split the version).
    /// </summary>
    public static string GetName(this BattleDecisionStrategy strategy)
    {
        var name = strategy.ToString().ToLowerInvariant();
        var sizeSeparator = name.LastIndexOf('_');
        return sizeSeparator < 0
            ? name
            : $"{name[..sizeSeparator].Replace('_', '.')}-{name[(sizeSeparator + 1)..]}";
    }
}
