using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.Teams.TeamPreviewOnLink;

public class TeamPreviewRoomParameters : IRoomParameterProvider
{
    public static readonly Parameter ShowTeamLinksPreview = new("tms", nameof(ShowTeamLinksPreview));

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = ShowTeamLinksPreview.Identifier,
            Name = ShowTeamLinksPreview.Name,
            NameKey = "parameter_name_is_showing_team_links_preview",
            DescriptionKey = "parameter_description_is_showing_team_links_preview",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = true.ToString()
        }
    ];
}
