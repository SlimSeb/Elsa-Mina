namespace ElsaMina.Commands.Games.Semantix;

public interface IEmbeddingService
{
    /// <summary>
    /// Returns the embedding vector for a word, using the permanent database cache first
    /// and calling the embeddings API only for never-seen words.
    /// Returns null when the API is unavailable or the daily call budget is exhausted.
    /// </summary>
    Task<float[]> GetEmbeddingAsync(string word, CancellationToken cancellationToken = default);
}
