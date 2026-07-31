using ElsaMina.DataAccess.Models;

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
    /// Maps owned holdings to catalogue entries, in shelf order: the order the user picked first,
    /// then largest first for the dolls they never moved. Ids missing from the drive are skipped,
    /// and an unreachable drive yields an empty list rather than an error, so profiles still render.
    /// </summary>
    Task<IReadOnlyList<Doll>> ResolveDollsAsync(IEnumerable<DollHolding> holdings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shifts a doll by <paramref name="offset"/> slots on the user's shelf, and renumbers all of their
    /// holdings so the new order sticks.
    /// </summary>
    Task<DollMoveResult> MoveDollAsync(string roomId, string userId, string dollId, int offset,
        CancellationToken cancellationToken = default);

    Task<bool> IsDollOwnedByUserAsync(string roomId, string userId, string dollId,
        CancellationToken cancellationToken = default);
}
