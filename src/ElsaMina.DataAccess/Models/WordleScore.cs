using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("WordleScores")]
public class WordleScore
{
    public string UserId { get; set; }
    public SavedUser User { get; set; }
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int CurrentStreak { get; set; }
    public int MaxStreak { get; set; }
    public int TotalGuesses { get; set; }
    public DateOnly? LastPlayedDate { get; set; }
}
