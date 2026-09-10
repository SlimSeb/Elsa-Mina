using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Dtos;

public sealed class BattleStateDto
{
    [JsonPropertyName("active")]
    public List<ActivePokemon> Active { get; set; } = new();

    [JsonPropertyName("forceSwitch")]
    [JsonConverter(typeof(ForceSwitchConverter))]
    public List<bool> ForceSwitch { get; set; } = new();

    [JsonPropertyName("teamPreview")]
    public bool TeamPreview { get; set; }

    [JsonPropertyName("wait")]
    public bool Wait { get; set; }

    [JsonPropertyName("side")]
    public Side Side { get; set; } = new();

    [JsonPropertyName("noCancel")]
    public bool NoCancel { get; set; }

    [JsonPropertyName("rqid")]
    public int Rqid { get; set; }
}
