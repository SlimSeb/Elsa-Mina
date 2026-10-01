namespace ElsaMina.Core.Services.Rooms.Parameters;

/// <summary>
/// Persists rooms and their parameter values. Core only depends on this port; the database implementation
/// lives in ElsaMina.DataAccess.
/// </summary>
public interface IRoomParameterRepository
{
    /// <summary>
    /// Creates the room's record if it does not exist yet, updates its title, and returns its stored parameter
    /// values keyed by parameter identifier.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> LoadOrCreateRoomAsync(string roomId, string roomTitle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a parameter value. Returns false when the value could not be saved.
    /// </summary>
    Task<bool> TrySaveParameterValueAsync(string roomId, string parameterIdentifier, string value,
        CancellationToken cancellationToken = default);
}
