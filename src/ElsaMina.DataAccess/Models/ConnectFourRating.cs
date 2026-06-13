using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("ConnectFourRatings")]
public class ConnectFourRating
{
    public string UserId { get; set; }
    public SavedUser User { get; set; }
    public int Rating { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
}
