using System.Text.Json.Serialization;

namespace ElsaMina.Core.Services.Smogon;

public class SmogonUsageInfoDto
{
    [JsonPropertyName("metagame")]
    public string Metagame { get; set; }

    [JsonPropertyName("cutoff")]
    public double Cutoff { get; set; }

    [JsonPropertyName("cutoff deviation")]
    public double CutoffDeviation { get; set; }

    [JsonPropertyName("team type")]
    public string TeamType { get; set; }

    [JsonPropertyName("number of battles")]
    public int NumberOfBattles { get; set; }
}
