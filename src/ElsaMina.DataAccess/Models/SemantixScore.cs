using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("SemantixScores")]
public class SemantixScore
{
    public string UserId { get; set; }
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int TotalGuesses { get; set; }
    public int BestGuessCount { get; set; }
    public int CurrentStreak { get; set; }
    public int MaxStreak { get; set; }
    public DateOnly? LastWonDate { get; set; }
}
