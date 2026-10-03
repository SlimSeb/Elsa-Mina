namespace ElsaMina.Core.Services.Rooms.Parameters;

/// <summary>
/// Contributes room parameters. Register implementations with <c>.As&lt;IRoomParameterProvider&gt;()</c>;
/// their definitions are listed in registration order, Core's first.
/// </summary>
public interface IRoomParameterProvider
{
    IEnumerable<IParameterDefinition> GetDefinitions();
}
