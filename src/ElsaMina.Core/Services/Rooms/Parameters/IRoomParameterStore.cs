namespace ElsaMina.Core.Services.Rooms.Parameters;

public interface IRoomParameterStore
{
    Task<string> GetValueAsync(Parameter parameter, CancellationToken cancellationToken = default);
    Task<bool> SetValueAsync(Parameter parameter, string value, CancellationToken cancellationToken = default);
    IRoom Room { get; set; }

    /// <summary>
    /// Loads the stored values of <paramref name="roomId"/>, keyed by parameter identifier.
    /// </summary>
    void Initialize(string roomId, IReadOnlyDictionary<string, string> storedValues);
}
