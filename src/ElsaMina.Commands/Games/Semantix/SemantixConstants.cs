namespace ElsaMina.Commands.Games.Semantix;

public static class SemantixConstants
{
    public const int EMBEDDING_DIMENSIONS = 256;

    // Daily API call budget: stays well under the Gemini free tier quota (~1,000 requests/day).
    public const int MAX_DAILY_API_CALLS = 900;

    public static readonly TimeSpan INACTIVITY_TIMEOUT = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan GUESS_COOLDOWN = TimeSpan.FromSeconds(3);

    // Cosine similarity → temperature (°C) mapping. With text-embedding-3-small,
    // unrelated words sit around 0.1-0.25 and near-synonyms around 0.6-0.8.
    // temperature = (similarity - SIMILARITY_FLOOR) * TEMPERATURE_SCALE, clamped.
    public const double SIMILARITY_FLOOR = 0.20;
    public const double TEMPERATURE_SCALE = 160;
    public const int MIN_TEMPERATURE = -30;
    public const int MAX_TEMPERATURE = 99;
    public const int WIN_TEMPERATURE = 100;
}
