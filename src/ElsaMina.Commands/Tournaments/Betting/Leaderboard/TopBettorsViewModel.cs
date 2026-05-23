using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Tournaments.Betting.Leaderboard;

public class TopBettorsViewModel : LocalizableViewModel
{
    public string Room { get; init; }
    public IEnumerable<TopBettorsEntry> TopList { get; init; }
}
