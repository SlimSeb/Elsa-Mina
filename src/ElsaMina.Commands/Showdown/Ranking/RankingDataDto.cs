using System.Drawing;
using ElsaMina.Core.Utils;
using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Showdown.Ranking;

public class RankingDataDto
{
    [JsonPropertyName("formatid")]
    public string FormatId { get; set; }
    [JsonPropertyName("w")]
    public int Wins { get; set; }
    [JsonPropertyName("l")]
    public int Losses { get; set; }
    [JsonPropertyName("t")]
    public int Ties { get; set; }
    [JsonPropertyName("gxe")]
    public double Gxe { get; set; }
    [JsonPropertyName("elo")]
    public double Elo { get; set; }
    [JsonPropertyName("first_played")]
    public int? FirstPlayed { get; set; }
    [JsonPropertyName("last_played")]
    public int? LastPlayed { get; set; }
    
    public double WinRate => Wins + Losses == 0 ? 0 : 100 * Wins / (double)(Wins + Losses);
    public Color GxeBasedColor => ShowdownColors.FromHsl(Gxe * 1.05, 85, 45);

}