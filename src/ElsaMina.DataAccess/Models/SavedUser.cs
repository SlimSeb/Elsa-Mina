using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("Users")]
public class SavedUser
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public ICollection<RoomUser> RoomData { get; set; } = [];
    public DateTimeOffset? RegisterDate { get; set; }
    public DateTimeOffset? LastOnline { get; set; } // to be renamed : this is more "last action datetime"
    public string LastSeenRoomId { get; set; }
    public UserAction LastSeenAction { get; set; }

    public FloodItScore FloodItScore { get; set; }
    public LightsOutScore LightsOutScore { get; set; }
    public VoltorbFlipLevel VoltorbFlipLevel { get; set; }
    public TwentyFortyEightScore TwentyFortyEightScore { get; set; }
    public ConnectFourRating ConnectFourRating { get; set; }
    public BattleshipRating BattleshipRating { get; set; }
    public ChessRating ChessRating { get; set; }
}