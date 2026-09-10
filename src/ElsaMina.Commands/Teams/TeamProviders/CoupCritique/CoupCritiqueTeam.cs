using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Teams.TeamProviders.CoupCritique;

public class CoupCritiqueTeam
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("export")]
    public string Export { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonPropertyName("user")]
    public CoupCritiqueUser User { get; set; }
}