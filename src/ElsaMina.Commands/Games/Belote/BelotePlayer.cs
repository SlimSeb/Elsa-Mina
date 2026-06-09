using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Belote;

public sealed class BelotePlayer
{
    public BelotePlayer(IUser user)
    {
        User = user;
    }

    public IUser User { get; private set; }
    public string UserId => User.UserId;
    public string Name => User.Name;

    /// <summary>
    /// Team index (0 or 1). Seats 0 and 2 form team 0, seats 1 and 3 form team 1, so partners sit across.
    /// Assigned once seating is shuffled at the start of the deal.
    /// </summary>
    public int Team { get; set; }

    /// <summary>
    /// True when this player has asked to be replaced by a substitute.
    /// </summary>
    public bool WantsSub { get; set; }

    /// <summary>
    /// Hands this seat over to another user, keeping the hand, captured pile and game state intact.
    /// </summary>
    public void SubstituteWith(IUser user)
    {
        User = user;
        WantsSub = false;
    }

    /// <summary>
    /// Cards currently in hand, sorted for display.
    /// </summary>
    public List<BeloteCard> Hand { get; } = [];

    /// <summary>
    /// Cards this player has captured by winning tricks.
    /// </summary>
    public List<BeloteCard> CapturedPile { get; } = [];

    /// <summary>
    /// Whether this player has already passed or taken in the current bidding round.
    /// </summary>
    public bool HasBid { get; set; }

    public bool IsTaker { get; set; }

    /// <summary>
    /// True when this player was dealt both the King and Queen of trump (belote-rebelote).
    /// </summary>
    public bool HasBelote { get; set; }
}
