using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.LeagueOfLegends;

public class LeagueHistoryGameViewModel
{
    public string ChampionName { get; set; }
    public int ChampionId { get; set; }
    public string ChampionIconUrl { get; set; }
    public bool Win { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public string KdaRatio { get; set; }
    public int Cs { get; set; }
    public string CsPerMinute { get; set; }
    public string QueueName { get; set; }
    public int DurationMinutes { get; set; }
    public DateTimeOffset GameDate { get; set; }
    public string FormattedDate { get; set; }
}

public class LeagueHistoryViewModel : LocalizableViewModel
{
    public string GameName { get; set; }
    public string TagLine { get; set; }
    public string Platform { get; set; }
    public List<LeagueHistoryGameViewModel> Games { get; set; } = [];
}
