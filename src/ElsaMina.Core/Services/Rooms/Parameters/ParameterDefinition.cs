namespace ElsaMina.Core.Services.Rooms.Parameters;

public class ParameterDefinition : IParameterDefinition
{
    public required string Identifier { get; set; }
    public string Name { get; set; }
    public required string NameKey { get; set; }
    public required string DescriptionKey { get; set; }
    public required RoomBotConfigurationType Type { get; set; }
    public required string DefaultValue { get; set; }
    public IEnumerable<EnumerationValue> PossibleValues { get; set; } // Only used for the enumeration type
    public Action<IRoom, string> OnUpdateAction { get; set; }
}