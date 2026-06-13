using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("FloodItScores")]
public class FloodItScore
{
    public string UserId { get; set; }
    public SavedUser User { get; set; }
    public int Level { get; set; }
    public int BestMoves { get; set; }
    public int TotalStars { get; set; }

}
