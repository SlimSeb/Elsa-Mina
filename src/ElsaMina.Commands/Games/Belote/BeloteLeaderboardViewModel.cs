using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Belote;

public class BeloteLeaderboardViewModel : LocalizableViewModel
{
    public IReadOnlyList<BeloteStats> Leaderboard { get; init; }
}
