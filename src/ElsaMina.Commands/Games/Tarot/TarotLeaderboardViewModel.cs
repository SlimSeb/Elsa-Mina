using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Tarot;

public class TarotLeaderboardViewModel : LocalizableViewModel
{
    public IReadOnlyList<TarotStats> Leaderboard { get; init; }
}
