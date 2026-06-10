namespace ElsaMina.Commands.Ai.Chat;

/// <summary>
/// Catalogue of the chat personalities the bot can switch between at runtime.
/// Each personality maps to a localized system-prompt resource key (the actual prompt
/// text lives in the <c>Ai.{locale}.resx</c> files). Prompt placeholders:
/// {0} = sender name, {1} = bot name, {2} = room name.
/// </summary>
public static class BotPersonalities
{
    public const BotPersonality DEFAULT = BotPersonality.Silly;

    private static readonly Dictionary<BotPersonality, string> PROMPT_KEYS =
        new()
        {
            [BotPersonality.Silly] = "personality_prompt_silly",
            [BotPersonality.Helpful] = "personality_prompt_helpful",
            [BotPersonality.Noir] = "personality_prompt_noir",
            [BotPersonality.Philosopher] = "personality_prompt_philosopher",
            [BotPersonality.Redditor] = "personality_prompt_redditor",
            [BotPersonality.Gremlin] = "personality_prompt_gremlin",
            [BotPersonality.Franchouillard] = "personality_prompt_franchouillard",
            [BotPersonality.Anxious] = "personality_prompt_anxious"
        };

    private static readonly Dictionary<string, BotPersonality> LOOKUP =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["silly"] = BotPersonality.Silly,
            ["default"] = BotPersonality.Silly,
            ["helpful"] = BotPersonality.Helpful,
            ["assistant"] = BotPersonality.Helpful,
            ["helper"] = BotPersonality.Helpful,
            ["smart"] = BotPersonality.Helpful,
            ["noir"] = BotPersonality.Noir,
            ["detective"] = BotPersonality.Noir,
            ["gumshoe"] = BotPersonality.Noir,
            ["philosopher"] = BotPersonality.Philosopher,
            ["philosophe"] = BotPersonality.Philosopher,
            ["philo"] = BotPersonality.Philosopher,
            ["redditor"] = BotPersonality.Redditor,
            ["reddit"] = BotPersonality.Redditor,
            ["geek"] = BotPersonality.Redditor,
            ["gremlin"] = BotPersonality.Gremlin,
            ["rawr"] = BotPersonality.Gremlin,
            ["uwu"] = BotPersonality.Gremlin,
            ["goblin"] = BotPersonality.Gremlin,
            ["franchouillard"] = BotPersonality.Franchouillard,
            ["frenchie"] = BotPersonality.Franchouillard,
            ["beret"] = BotPersonality.Franchouillard,
            ["baguette"] = BotPersonality.Franchouillard,
            ["anxious"] = BotPersonality.Anxious,
            ["nervous"] = BotPersonality.Anxious,
            ["shy"] = BotPersonality.Anxious
        };

    /// <summary>
    /// Comma-separated list of the primary names users can pass to switch personality.
    /// </summary>
    public static string AvailableNames => "silly, helpful, noir, philosopher, redditor, gremlin, franchouillard, anxious";

    public static string GetPromptKey(BotPersonality personality) => PROMPT_KEYS[personality];

    public static string GetLabel(BotPersonality personality) => personality.ToString().ToLowerInvariant();

    public static bool TryParse(string value, out BotPersonality personality)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return LOOKUP.TryGetValue(value.Trim(), out personality);
        }

        personality = DEFAULT;
        return false;
    }
}
