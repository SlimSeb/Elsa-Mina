using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Chess;

public class ChessLeaderboardViewModel : LocalizableViewModel
{
    public IReadOnlyList<ChessRating> Leaderboard { get; init; }
}
