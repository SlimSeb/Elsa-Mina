using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

public sealed class TarotPlayer
{
    public TarotPlayer(IUser user)
    {
        User = user;
    }

    public IUser User { get; }
    public string UserId => User.UserId;
    public string Name => User.Name;

    /// <summary>
    /// Cards currently in hand, sorted for display.
    /// </summary>
    public List<TarotCard> Hand { get; } = [];

    /// <summary>
    /// Cards this player has captured by winning tricks (plus a kept Excuse, if any).
    /// </summary>
    public List<TarotCard> CapturedPile { get; } = [];

    public TarotBid Bid { get; set; } = TarotBid.Pass;
    public bool HasBid { get; set; }

    public bool IsTaker { get; set; }

    /// <summary>
    /// True when this player holds the king called by the taker (5-player partner). Secret until revealed.
    /// </summary>
    public bool IsPartner { get; set; }
}
