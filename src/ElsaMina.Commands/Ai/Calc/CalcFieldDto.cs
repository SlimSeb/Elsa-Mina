using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Ai.Calc;

/// <summary>
/// Global field conditions for a damage calculation (weather, terrain, game type, sides).
/// </summary>
public class CalcFieldDto
{
    [JsonPropertyName("gameType")]
    public string GameType { get; set; }

    [JsonPropertyName("weather")]
    public string Weather { get; set; }

    [JsonPropertyName("terrain")]
    public string Terrain { get; set; }

    [JsonPropertyName("attackerSide")]
    public CalcSideDto AttackerSide { get; set; }

    [JsonPropertyName("defenderSide")]
    public CalcSideDto DefenderSide { get; set; }
}
