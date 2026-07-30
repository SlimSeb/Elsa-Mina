namespace ElsaMina.Commands.Dolls;

public interface IDollService
{
    /// <summary>
    /// The whole doll catalogue, keyed by doll id, read from the Google Drive and cached.
    /// Throws when the drive cannot be read.
    /// </summary>
    Task<IReadOnlyDictionary<string, Doll>> GetCatalogueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the cache and reads the drive again, to pick up newly uploaded sprites.
    /// </summary>
    Task<IReadOnlyDictionary<string, Doll>> RefreshCatalogueAsync(CancellationToken cancellationToken = default);

    Task<Doll> GetDollAsync(string dollId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps owned doll ids to catalogue entries, largest first. Ids missing from the drive are skipped,
    /// and an unreachable drive yields an empty list rather than an error, so profiles still render.
    /// </summary>
    Task<IReadOnlyList<Doll>> ResolveDollsAsync(IEnumerable<string> dollIds,
        CancellationToken cancellationToken = default);

    Task<bool> IsDollOwnedByUserAsync(string roomId, string userId, string dollId,
        CancellationToken cancellationToken = default);
}
