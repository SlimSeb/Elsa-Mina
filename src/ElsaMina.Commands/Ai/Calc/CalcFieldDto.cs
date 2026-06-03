using Newtonsoft.Json;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// Global field conditions for a damage calculation (weather, terrain, game type, sides).
/// </summary>
public class CalcFieldDto
{
    [JsonProperty("gameType")]
    public string GameType { get; set; }

    [JsonProperty("weather")]
    public string Weather { get; set; }

    [JsonProperty("terrain")]
    public string Terrain { get; set; }

    [JsonProperty("attackerSide")]
    public CalcSideDto AttackerSide { get; set; }

    [JsonProperty("defenderSide")]
    public CalcSideDto DefenderSide { get; set; }
}
