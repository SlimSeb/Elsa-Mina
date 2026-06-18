using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("BattleshipRatings")]
public class BattleshipRating
{
    public string UserId { get; set; }
    public SavedUser User { get; set; }
    public int Rating { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
}
