using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Smogon;

public class SmogonUsageDataDto
{
    [JsonPropertyName("info")]
    public SmogonUsageInfoDto Info { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, SmogonPokemonUsageDataDto> Data { get; set; }
}
