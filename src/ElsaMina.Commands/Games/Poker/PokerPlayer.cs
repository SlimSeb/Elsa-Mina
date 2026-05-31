using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Poker;

public sealed class PokerPlayer
{
    public PokerPlayer(IUser user, long stack)
    {
        User = user;
        Stack = stack;
    }

    public IUser User { get; }
    public string UserId => User.UserId;
    public string Name => User.Name;

    /// <summary>
    /// The two private hole cards dealt to this player.
    /// </summary>
    public List<PokerCard> HoleCards { get; } = [];

    /// <summary>
    /// Chips currently sitting in front of the player (not yet wagered).
    /// </summary>
    public long Stack { get; set; }

    /// <summary>
    /// Total chips this player has pushed into the pot during the whole hand.
    /// </summary>
    public long Committed { get; set; }

    /// <summary>
    /// Chips this player has pushed into the pot during the current betting round.
    /// </summary>
    public long RoundBet { get; set; }

    public bool HasFolded { get; set; }

    /// <summary>
    /// True once the player has no chips left but is still contesting the pot.
    /// </summary>
    public bool IsAllIn => Stack == 0 && !HasFolded;

    /// <summary>
    /// True once the player has acted at least once in the current betting round.
    /// </summary>
    public bool HasActed { get; set; }

    /// <summary>
    /// The best five-card hand for this player, computed at showdown.
    /// </summary>
    public PokerHandEvaluation Evaluation { get; set; }

    /// <summary>
    /// Chips this player collected from the pot(s) at showdown.
    /// </summary>
    public long Winnings { get; set; }

    /// <summary>
    /// True when this player is still able to act (not folded and not all-in).
    /// </summary>
    public bool CanAct => !HasFolded && !IsAllIn;
}
