using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Profile;

public class ProfileViewModel : LocalizableViewModel
{
    public string UserId { get; init; }
    public string UserName { get; init; }
    public string Avatar { get; init; }
    public char UserRoomRank { get; init; }
    public string Status { get; init; }
    public string Title { get; init; }
    public string ProfileEmoji { get; init; }
    public string ProfileBackgroundColor { get; init; }
    public string ProfileBorderColor { get; init; }
    public IEnumerable<Badge> Badges { get; init; }
    public DateTimeOffset RegisterDate { get; init; }
    public RankingDataDto BestRanking { get; init; }
    public TournamentRecord TournamentRecord { get; init; }
    public TimeSpan PlayTime { get; init; }
    public GameRecords GameRecords { get; init; }
}