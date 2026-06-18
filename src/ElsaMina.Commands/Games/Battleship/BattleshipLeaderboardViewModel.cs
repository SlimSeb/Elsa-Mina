using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Battleship;

public class BattleshipLeaderboardViewModel : LocalizableViewModel
{
    public IReadOnlyList<BattleshipRating> Leaderboard { get; init; }
}
