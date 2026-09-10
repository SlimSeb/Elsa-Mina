using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Teams.TeamProviders.CoupCritique;

public class CoupCritiqueResponse
{
    [JsonPropertyName("team")]
    public CoupCritiqueTeam Team { get; set; }
}