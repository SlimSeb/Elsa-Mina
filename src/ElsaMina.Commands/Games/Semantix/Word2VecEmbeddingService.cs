using ElsaMina.Logging;

namespace ElsaMina.Commands.Games.Semantix;

/// <summary>
/// Loads a filtered French word2vec model (frWac skip-gram, ~30k common words)
/// from a binary file into memory. No API, no cost, no rate limits — and it
/// captures contextual/conceptual similarity (e.g. "demander" ~ "répondre")
/// the way the original Cemantix does.
/// </summary>
public class Word2VecEmbeddingService : IEmbeddingService
{
    private const string DATA_DIRECTORY_NAME = "Data";

    private readonly Lock _loadLock = new();
    private Dictionary<string, float[]> _vectors;

    public Task<float[]> GetEmbeddingAsync(string word, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return Task.FromResult<float[]>(null);
        }

        EnsureLoaded();
        _vectors.TryGetValue(word.Trim().ToLowerInvariant(), out var vector);
        return Task.FromResult(vector);
    }

    private void EnsureLoaded()
    {
        if (_vectors != null)
        {
            return;
        }

        lock (_loadLock)
        {
            if (_vectors != null)
            {
                return;
            }

            _vectors = LoadVectors();
        }
    }

    private static Dictionary<string, float[]> LoadVectors()
    {
        var path = Path.Join(DATA_DIRECTORY_NAME, SemantixConstants.VECTORS_FILE);
        var vectors = new Dictionary<string, float[]>();

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            var count = reader.ReadInt32();
            var dim = reader.ReadInt32();

            for (var i = 0; i < count; i++)
            {
                var wordLength = reader.ReadInt32();
                var word = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(wordLength));
                var vector = new float[dim];
                for (var d = 0; d < dim; d++)
                {
                    vector[d] = (float)reader.ReadHalf();
                }

                vectors[word] = vector;
            }

            Log.Information("Loaded {0} Semantix word2vec vectors ({1} dims)", vectors.Count, dim);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load Semantix word2vec vectors from {0}", path);
        }

        return vectors;
    }
}
