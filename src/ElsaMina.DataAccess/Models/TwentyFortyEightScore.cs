using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("TwentyFortyEightScores")]
public class TwentyFortyEightScore
{
    public string UserId { get; set; }
    public SavedUser User { get; set; }
    public int Wins { get; set; }
    public int BestScore { get; set; }

}
