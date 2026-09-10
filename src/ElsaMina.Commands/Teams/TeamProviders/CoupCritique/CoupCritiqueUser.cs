using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Teams.TeamProviders.CoupCritique;

public class CoupCritiqueUser
{
    [JsonPropertyName("username")]
    public string UserName { get; set; }
}