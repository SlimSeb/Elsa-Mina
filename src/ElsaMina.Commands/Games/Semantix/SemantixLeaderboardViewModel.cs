using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Semantix;

public class SemantixLeaderboardViewModel : LocalizableViewModel
{
    public IReadOnlyList<SemantixScore> Leaderboard { get; init; }
}
