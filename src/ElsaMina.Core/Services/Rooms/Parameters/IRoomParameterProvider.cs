namespace ElsaMina.Core.Services.Rooms.Parameters;

/// <summary>
/// Fournit des paramètres de room. Enregistrer les implémentations avec <c>.As&lt;IRoomParameterProvider&gt;()</c> ;
/// les définitions sont listées dans l'ordre d'enregistrement, celles de Core en premier
/// </summary>
public interface IRoomParameterProvider
{
    IEnumerable<IParameterDefinition> GetDefinitions();
}
