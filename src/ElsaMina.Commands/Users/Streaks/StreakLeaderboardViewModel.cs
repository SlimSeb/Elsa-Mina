using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Users.Streaks;

public class StreakLeaderboardViewModel : LocalizableViewModel
{
    public string Room { get; init; }
    public IEnumerable<StreakLeaderboardEntry> TopList { get; init; }
}
