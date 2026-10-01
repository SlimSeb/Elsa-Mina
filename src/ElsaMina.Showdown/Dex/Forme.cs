using System.Text.Json.Serialization;

namespace ElsaMina.Showdown.Dex;

public class Forme
{
    [JsonPropertyName("region")]
    public string Region { get; set; }

    [JsonPropertyName("name")]
    public Name Name { get; set; }
}
