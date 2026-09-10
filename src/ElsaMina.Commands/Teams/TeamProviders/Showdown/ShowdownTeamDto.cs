using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Teams.TeamProviders.Showdown;

public class ShowdownTeamDto
{
    [JsonPropertyName("team")]
    public string PackedTeam { get; set; }
    
    [JsonPropertyName("ownerid")]
    public string OwnerId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }
}