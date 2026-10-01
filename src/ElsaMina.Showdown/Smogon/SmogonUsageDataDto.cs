using System.Text.Json.Serialization;

namespace ElsaMina.Showdown.Smogon;

public class SmogonUsageDataDto
{
    [JsonPropertyName("info")]
    public SmogonUsageInfoDto Info { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, SmogonPokemonUsageDataDto> Data { get; set; }
}
