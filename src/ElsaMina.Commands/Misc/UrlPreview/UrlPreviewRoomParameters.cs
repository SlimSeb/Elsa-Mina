using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.Misc.UrlPreview;

public class UrlPreviewRoomParameters : IRoomParameterProvider
{
    public static readonly Parameter ShowUrlPreview = new("urlp", nameof(ShowUrlPreview));

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = ShowUrlPreview.Identifier,
            Name = ShowUrlPreview.Name,
            NameKey = "parameter_name_is_showing_url_preview",
            DescriptionKey = "parameter_description_is_showing_url_preview",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = false.ToString()
        }
    ];
}
