namespace ElsaMina.Commands.Games.Semantix;

public static class SemantixConstants
{
    public const string VECTORS_FILE = "semantix_vectors_fr.bin";

    public static readonly TimeSpan INACTIVITY_TIMEOUT = TimeSpan.FromMinutes(5);

    // Cosine similarity → temperature (°C) mapping.
    // With a context-trained word2vec (frWac skip-gram), unrelated words sit near 0,
    // loosely related ~0.2-0.35, related ~0.4-0.55 and synonyms ~0.6+. The distribution
    // is well spread (unlike LLM embeddings), so a near-linear mapping over [FLOOR, CEILING]
    // works well.
    //   norm = clamp01((similarity - FLOOR) / (CEILING - FLOOR))
    //   temperature = norm^GAMMA * (MAX - MIN) + MIN
    // Raw similarity is logged per guess at Information level to ease tuning.
    public const double SIMILARITY_FLOOR = 0.0;
    public const double SIMILARITY_CEILING = 0.60;
    public const double TEMPERATURE_GAMMA = 1.0;
    public const int MIN_TEMPERATURE = -30;
    public const int MAX_TEMPERATURE = 99;
    public const int WIN_TEMPERATURE = 100;
}
