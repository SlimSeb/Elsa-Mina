using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("LightsOutScores")]
public class LightsOutScore
{
    public string UserId { get; set; }
    public SavedUser User { get; set; }
    public int Level { get; set; }
    public int BestMoves { get; set; }
    public int TotalStars { get; set; }

}
