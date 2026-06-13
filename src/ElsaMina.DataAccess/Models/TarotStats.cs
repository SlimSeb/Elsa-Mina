using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("TarotStats")]
public class TarotStats
{
    public string UserId { get; set; }
    public SavedUser User { get; set; }

    /// <summary>
    /// Cumulative net score across every deal, stored in half-points (real value × 2) to stay an exact integer.
    /// </summary>
    public int TotalScoreHalfPoints { get; set; }

    public int GamesPlayed { get; set; }

    /// <summary>
    /// Deals where this player ended with a positive net score.
    /// </summary>
    public int Wins { get; set; }

    public int TimesTaker { get; set; }

    /// <summary>
    /// Deals where this player was the taker and the contract was made.
    /// </summary>
    public int TakerWins { get; set; }
}
