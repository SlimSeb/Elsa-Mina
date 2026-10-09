namespace ElsaMina.Core.Services.Rooms.Parameters;

public interface IRoomParameterStore
{
    Task<string> GetValueAsync(Parameter parameter, CancellationToken cancellationToken = default);
    Task<bool> SetValueAsync(Parameter parameter, string value, CancellationToken cancellationToken = default);
    IRoom Room { get; set; }

    /// <summary>
    /// Charge les valeurs enregistrées de <paramref name="roomId"/>, indexées par identifiant de paramètre
    /// </summary>
    void Initialize(string roomId, IReadOnlyDictionary<string, string> storedValues);
}
