using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

/// <summary>
/// User data depending on the room
/// </summary>
[Table("RoomUsers")]
public class RoomUser
{
    public string Id { get; set; }
    public string RoomId { get; set; }
    public string Avatar { get; set; }
    public string Title { get; set; }
    public string JoinPhrase { get; set; }
    public string ProfileEmoji { get; set; }
    public string ProfileBackgroundColor { get; set; }
    public TimeSpan PlayTime { get; set; }
    public TournamentRecord TournamentRecord { get; set; }
    public ICollection<BadgeHolding> Badges { get; set; } = new HashSet<BadgeHolding>();
    public SavedUser User { get; set; }
}