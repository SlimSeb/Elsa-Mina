namespace ElsaMina.Battles.Strategies;

public static class BattleDecisionStrategyExtensions
{
    private const string SEARCH_SUFFIX = "_Search";
    private const string SEARCH_NAME_SUFFIX = "+search";

    /// <summary>
    /// The name staff type to pick the strategy: "damagecalc", "gladionv1-22m" for GladionV1_22M (the last underscore
    /// comes before the size, the others split a minor version: GladionV1_1_22M would be "gladionv1.1-22m"), and
    /// "gladionv1-22m+search" for the search variant GladionV1_22M_Search.
    /// </summary>
    public static string GetName(this BattleDecisionStrategy strategy)
    {
        var search = strategy.IsSearch();
        var text = strategy.ToString();
        var name = (search ? text[..^SEARCH_SUFFIX.Length] : text).ToLowerInvariant();
        var sizeSeparator = name.LastIndexOf('_');
        if (sizeSeparator >= 0)
        {
            name = $"{name[..sizeSeparator].Replace('_', '.')}-{name[(sizeSeparator + 1)..]}";
        }

        return search ? name + SEARCH_NAME_SUFFIX : name;
    }

    /// <summary>
    /// A model's search variant (GladionV1_22M_Search): the same model, searching one turn ahead.
    /// </summary>
    public static bool IsSearch(this BattleDecisionStrategy strategy) =>
        strategy.ToString().EndsWith(SEARCH_SUFFIX, StringComparison.Ordinal);

    /// <summary>
    /// The search variant of a model's strategy, or null when it has none.
    /// </summary>
    public static BattleDecisionStrategy? WithSearch(this BattleDecisionStrategy strategy) =>
        !strategy.IsSearch() && Enum.TryParse<BattleDecisionStrategy>(strategy + SEARCH_SUFFIX, out var withSearch)
            ? withSearch
            : null;
}
