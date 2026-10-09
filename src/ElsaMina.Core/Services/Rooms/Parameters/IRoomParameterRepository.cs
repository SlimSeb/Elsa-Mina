namespace ElsaMina.Core.Services.Rooms.Parameters;

/// <summary>
/// Persiste les rooms et les valeurs de leurs paramètres. Core dépend juste de ce port, l'implémentation bdd
/// est dans ElsaMina.DataAccess
/// </summary>
public interface IRoomParameterRepository
{
    /// <summary>
    /// Crée la room en bdd si elle existe pas encore, met à jour son titre, et renvoie les valeurs de ses paramètres
    /// indexées par identifiant de paramètre
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> LoadOrCreateRoomAsync(string roomId, string roomTitle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enregistre la valeur d'un paramètre. Renvoie false si la sauvegarde a pas marché
    /// </summary>
    Task<bool> TrySaveParameterValueAsync(string roomId, string parameterIdentifier, string value,
        CancellationToken cancellationToken = default);
}
