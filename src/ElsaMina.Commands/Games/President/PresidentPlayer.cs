using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.President;

public sealed class PresidentPlayer
{
    public PresidentPlayer(IUser user)
    {
        User = user;
    }

    public IUser User { get; private set; }
    public string UserId => User.UserId;
    public string Name => User.Name;

    /// <summary>
    /// True when this player has asked to be replaced by a substitute.
    /// </summary>
    public bool WantsSub { get; set; }

    /// <summary>
    /// Hands this seat over to another user, keeping the hand and game state intact.
    /// </summary>
    public void SubstituteWith(IUser user)
    {
        User = user;
        WantsSub = false;
    }

    /// <summary>
    /// Cards currently in hand, sorted for display.
    /// </summary>
    public List<PresidentCard> Hand { get; } = [];

    /// <summary>
    /// Role earned at the end of the previous round; drives the card exchange of the current one.
    /// </summary>
    public PresidentRole Role { get; set; } = PresidentRole.None;

    /// <summary>
    /// 1-based position in which this player emptied their hand this round, or 0 while still playing.
    /// </summary>
    public int FinishPosition { get; set; }

    /// <summary>
    /// True once this player has passed on the current trick; cleared when the pile is taken.
    /// </summary>
    public bool HasPassed { get; set; }

    /// <summary>
    /// Cumulative points over all played rounds.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Number of cards this player still owes during the exchange phase (president and
    /// vice-president give-backs). 0 when nothing is owed.
    /// </summary>
    public int CardsToGive { get; set; }

    /// <summary>
    /// Cards currently selected (but not yet confirmed) for the exchange give-back.
    /// </summary>
    public List<PresidentCard> PendingGives { get; } = [];

    /// <summary>
    /// Cards received during the current exchange, shown privately on the player's hand page.
    /// </summary>
    public List<PresidentCard> ReceivedCards { get; } = [];

    /// <summary>
    /// Cards automatically handed over during the current exchange (scum and vice-scum gifts),
    /// shown privately on the giver's hand page.
    /// </summary>
    public List<PresidentCard> GivenCards { get; } = [];
}
