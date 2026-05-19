using Newtonsoft.Json;

namespace ElsaMina.Battles.Dtos;

public sealed class BattleStateDto
{
    [JsonProperty("active")]
    public List<ActivePokemon> Active { get; set; } = new();

    [JsonProperty("forceSwitch")]
    [JsonConverter(typeof(ForceSwitchConverter))]
    public List<bool> ForceSwitch { get; set; } = new();

    [JsonProperty("teamPreview", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool TeamPreview { get; set; }

    [JsonProperty("wait", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool Wait { get; set; }

    [JsonProperty("side")]
    public Side Side { get; set; } = new();

    [JsonProperty("noCancel", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool NoCancel { get; set; }

    [JsonProperty("rqid")]
    public int Rqid { get; set; }
}
