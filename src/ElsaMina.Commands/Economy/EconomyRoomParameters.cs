using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.Economy;

public class EconomyRoomParameters : IRoomParameterProvider
{
    public static readonly Parameter BucksEnabled = new("bck", nameof(BucksEnabled));

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = BucksEnabled.Identifier,
            Name = BucksEnabled.Name,
            NameKey = "parameter_name_bucks_enabled",
            DescriptionKey = "parameter_description_bucks_enabled",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = false.ToString()
        }
    ];
}
