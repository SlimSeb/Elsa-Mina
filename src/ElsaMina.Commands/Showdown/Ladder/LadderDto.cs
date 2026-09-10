using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Showdown.Ladder;

public class LadderDto
{
    [JsonPropertyName("formatid")]
    public string FormatId { get; set; }
    [JsonPropertyName("format")]
    public string Format { get; set; }
    [JsonPropertyName("toplist")]
    public IEnumerable<LadderPlayerDto> TopList { get; set; }
}

public class LadderPlayerDto
{
    [JsonPropertyName("userid")]
    public string UserId { get; set; }
    [JsonPropertyName("username")]
    public string Username { get; set; }
    [JsonPropertyName("w")]
    public int Wins { get; set; }
    [JsonPropertyName("l")]
    public int Losses { get; set; }
    [JsonPropertyName("t")]
    public int Ties { get; set; }
    [JsonPropertyName("elo")]
    public double Elo { get; set; }
    [JsonPropertyName("gxe")]
    public double Gxe { get; set; }

    public double WinRate => Wins + Losses == 0 ? 0 : 100 * Wins / (double)(Wins + Losses);
    public int Index { get; set; }
    public int InnerIndex { get; set; }
    public int? EloDifference { get; set; }
    public int? IndexDifference { get; set; }
    public int? InnerIndexDifference { get; set; }
}
