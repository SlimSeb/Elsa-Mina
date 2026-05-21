using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class MatchDto
{
    [JsonProperty("info")]
    public MatchInfoDto Info { get; set; }
}
