namespace ElsaMina.Commands.Games.Scattergories;

public static class ScattergoriesConstants
{
    /// <summary>Number of letters played before the game ends.</summary>
    public const int ROUNDS_COUNT = 5;

    /// <summary>Duration of a single round.</summary>
    public static readonly TimeSpan ROUND_DURATION = TimeSpan.FromSeconds(30);

    /// <summary>How long before the end of a round players get a warning.</summary>
    public static readonly TimeSpan WARNING_THRESHOLD = TimeSpan.FromSeconds(10);

    /// <summary>Pause between the round summary and the next round.</summary>
    public static readonly TimeSpan INTER_ROUND_DELAY = TimeSpan.FromSeconds(4);

    /// <summary>
    /// A letter is only eligible to be drawn if at least this many Pokémon can be named with it,
    /// so players are never handed a near-impossible letter.
    /// </summary>
    public const int MIN_POKEMON_PER_LETTER = 8;
}
