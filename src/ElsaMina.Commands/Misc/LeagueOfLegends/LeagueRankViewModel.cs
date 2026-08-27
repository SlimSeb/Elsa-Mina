using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class LeagueRankQueueViewModel
{
    public string QueueType { get; set; }
    public string QueueName { get; set; }
    public string Tier { get; set; }
    public string Rank { get; set; }
    public int LeaguePoints { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int WinRate { get; set; }
    public string FormattedRank { get; set; }
    public string EmblemUrl { get; set; }
    public string TierColor { get; set; }
    public bool IsUnranked { get; set; }
}

public class LeagueRankViewModel : LocalizableViewModel
{
    public string GameName { get; set; }
    public string TagLine { get; set; }
    public string Platform { get; set; }
    public LeagueRankQueueViewModel SoloQueue { get; set; }
    public LeagueRankQueueViewModel FlexQueue { get; set; }
}
