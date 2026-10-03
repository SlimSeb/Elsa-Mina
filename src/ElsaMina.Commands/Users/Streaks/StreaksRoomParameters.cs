using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.Users.Streaks;

public class StreaksRoomParameters : IRoomParameterProvider
{
    public static readonly Parameter StreaksEnabled = new("stk", nameof(StreaksEnabled));

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = StreaksEnabled.Identifier,
            Name = StreaksEnabled.Name,
            NameKey = "parameter_name_streaks_enabled",
            DescriptionKey = "parameter_description_streaks_enabled",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = true.ToString()
        }
    ];
}
