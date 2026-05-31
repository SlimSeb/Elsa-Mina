using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Economy;

public class MoneyLeaderboardViewModel : LocalizableViewModel
{
    public IReadOnlyList<KeyValuePair<string, long>> Leaderboard { get; init; }
}
