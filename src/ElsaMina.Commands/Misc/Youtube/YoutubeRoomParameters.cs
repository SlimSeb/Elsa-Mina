using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.Misc.Youtube;

public class YoutubeRoomParameters : IRoomParameterProvider
{
    public static readonly Parameter ShowYoutubeLinkPreview = new("ytl", nameof(ShowYoutubeLinkPreview));

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = ShowYoutubeLinkPreview.Identifier,
            Name = ShowYoutubeLinkPreview.Name,
            NameKey = "parameter_name_is_showing_youtube_link_preview",
            DescriptionKey = "parameter_description_is_showing_youtube_link_preview",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = true.ToString()
        }
    ];
}
