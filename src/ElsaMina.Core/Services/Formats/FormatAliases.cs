namespace ElsaMina.Core.Services.Formats;

/// <summary>
/// Teams are saved under the format staff type with -add-team, which can be the server's id ("gen9ou",
/// "gen9nationaldex") as well as a shorter name for it: without the current generation ("ou") or with a usual short
/// tier name ("natdex").
/// </summary>
public static class FormatAliases
{
    private const string CURRENT_GENERATION = "gen9";
    private const string GENERATION_PREFIX = "gen";
    private const int GENERATION_LENGTH = 4;

    private static readonly Dictionary<string, string> SHORT_TIER_NAMES = new()
    {
        ["nationaldex"] = "natdex",
        ["almostanyability"] = "aaa",
        ["anythinggoes"] = "ag"
    };

    private static readonly Dictionary<string, string> FULL_TIER_NAMES =
        SHORT_TIER_NAMES.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    /// The format ids a team for this format can be saved under, the format id itself first: "gen9nationaldex" gives
    /// "gen9nationaldex", "nationaldex", "gen9natdex" and "natdex"; "gen8ou" gives only "gen8ou".
    /// </summary>
    public static IReadOnlyList<string> GetEquivalentFormatIds(string formatId)
    {
        if (string.IsNullOrEmpty(formatId))
        {
            return [];
        }

        var (generation, tier) = SplitGeneration(formatId);
        if (tier.Length == 0)
        {
            return [formatId];
        }

        var fullTier = FULL_TIER_NAMES.GetValueOrDefault(tier, tier);
        string[] tierNames = SHORT_TIER_NAMES.TryGetValue(fullTier, out var shortTier)
            ? [fullTier, shortTier]
            : [fullTier];
        var formatIds = new List<string> { formatId };
        foreach (var tierName in tierNames)
        {
            formatIds.Add(generation + tierName);
            if (generation == CURRENT_GENERATION)
            {
                formatIds.Add(tierName);
            }
        }

        return formatIds.Distinct().ToList();
    }

    // Showdown's generations have one digit: "gen91v1" is [Gen 9] 1v1
    private static (string Generation, string Tier) SplitGeneration(string formatId)
    {
        if (formatId.Length >= GENERATION_LENGTH && formatId.StartsWith(GENERATION_PREFIX, StringComparison.Ordinal) &&
            char.IsAsciiDigit(formatId[GENERATION_LENGTH - 1]))
        {
            return (formatId[..GENERATION_LENGTH], formatId[GENERATION_LENGTH..]);
        }

        return (CURRENT_GENERATION, formatId);
    }
}
