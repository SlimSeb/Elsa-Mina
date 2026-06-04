namespace ElsaMina.Commands.Games.Semantix;

public static class SemantixConstants
{
    public const int EMBEDDING_DIMENSIONS = 256;

    // Daily API call budget: stays well under the Gemini free tier quota (~1,000 requests/day).
    public const int MAX_DAILY_API_CALLS = 900;

    public static readonly TimeSpan INACTIVITY_TIMEOUT = TimeSpan.FromMinutes(5);

    // Cosine similarity → temperature (°C) mapping.
    // LLM embeddings (unlike word2vec) keep ALL words in a narrow, high cosine band
    // (~0.55 for unrelated words up to ~0.85 for very close ones), so a plain linear
    // scale bunches everything near the top. We normalize the useful band
    // [FLOOR, CEILING] to [0, 1] then apply a gamma > 1 curve so the large "loosely
    // related" cluster stays cold and only genuinely close words heat up.
    //   norm = clamp01((similarity - FLOOR) / (CEILING - FLOOR))
    //   temperature = norm^GAMMA * (MAX - MIN) + MIN
    // These three values are the dials to tune from real Gemini data (raw similarity
    // is logged per guess at Information level).
    // Calibrated from real Gemini data: with gemini-embedding-001, totally unrelated
    // words sit around 0.67-0.73 cosine, so the floor is high to keep them cold.
    public const double SIMILARITY_FLOOR = 0.65;
    public const double SIMILARITY_CEILING = 0.85;
    public const double TEMPERATURE_GAMMA = 1.8;
    public const int MIN_TEMPERATURE = -30;
    public const int MAX_TEMPERATURE = 99;
    public const int WIN_TEMPERATURE = 100;
}
