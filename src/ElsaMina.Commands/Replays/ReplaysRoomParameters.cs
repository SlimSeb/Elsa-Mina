using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.Replays;

public class ReplaysRoomParameters : IRoomParameterProvider
{
    public static readonly Parameter ShowReplaysPreview = new("rpl", nameof(ShowReplaysPreview));

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = ShowReplaysPreview.Identifier,
            Name = ShowReplaysPreview.Name,
            NameKey = "parameter_name_is_showing_replays_preview",
            DescriptionKey = "parameter_description_is_showing_replays_preview",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = true.ToString()
        }
    ];
}
