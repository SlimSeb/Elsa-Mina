using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// The order of every one-hot and per-name block in the features (poke-env enum member names).
/// </summary>
public sealed class EncoderSpecOrderings
{
    [JsonPropertyName("types")]
    public List<string> Types { get; set; } = [];

    [JsonPropertyName("statuses")]
    public List<string> Statuses { get; set; } = [];

    [JsonPropertyName("boosts")]
    public List<string> Boosts { get; set; } = [];

    [JsonPropertyName("stats")]
    public List<string> Stats { get; set; } = [];

    [JsonPropertyName("effects")]
    public List<string> Effects { get; set; } = [];

    [JsonPropertyName("weathers")]
    public List<string> Weathers { get; set; } = [];

    [JsonPropertyName("fields")]
    public List<string> Fields { get; set; } = [];

    [JsonPropertyName("side_conditions")]
    public List<string> SideConditions { get; set; } = [];

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];
}
