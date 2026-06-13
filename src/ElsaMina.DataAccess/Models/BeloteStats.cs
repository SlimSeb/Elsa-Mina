using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("BeloteStats")]
public class BeloteStats
{
    public string UserId { get; set; }
    public SavedUser User { get; set; }

    /// <summary>
    /// Cumulative round score across every deal (whole points).
    /// </summary>
    public int TotalScore { get; set; }

    public int GamesPlayed { get; set; }

    /// <summary>
    /// Deals where this player's team outscored the opposing team.
    /// </summary>
    public int Wins { get; set; }

    public int TimesTaker { get; set; }

    /// <summary>
    /// Deals where this player was the taker and the contract was made.
    /// </summary>
    public int TakerWins { get; set; }
}
