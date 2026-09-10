using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class MatchDto
{
    [JsonPropertyName("info")]
    public MatchInfoDto Info { get; set; }
}
