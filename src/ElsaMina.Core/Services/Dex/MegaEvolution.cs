using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Dex;

public class MegaEvolution
{
    [JsonPropertyName("orbe")]
    public string Orb { get; set; }

    [JsonPropertyName("sprites")]
    public Sprite Sprites { get; set; }
}
