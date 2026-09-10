using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Teams.TeamProviders.Pokepaste;

public class PokepasteTeam
{
    [JsonPropertyName("author")]
    public string Author { get; set; }
    [JsonPropertyName("notes")]
    public string Notes { get; set; }
    [JsonPropertyName("paste")]
    public string Paste { get; set; }
    [JsonPropertyName("title")]
    public string Title { get; set; }
}