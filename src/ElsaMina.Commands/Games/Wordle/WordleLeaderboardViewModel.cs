using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Wordle;

public class WordleLeaderboardViewModel : LocalizableViewModel
{
    public IReadOnlyList<WordleScore> Leaderboard { get; init; }
}
