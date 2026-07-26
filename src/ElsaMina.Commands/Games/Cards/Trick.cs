namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// A single trick: the cards played by each seat in order, and the logic to find who is winning it.
/// Tarot and belote run the same algorithm over different cards, so it lives here once and each game
/// supplies an <see cref="ITrickRules{TCard}"/>.
/// </summary>
/// <typeparam name="TPlayer">The game's own seat type.</typeparam>
/// <typeparam name="TCard">The game's own card type.</typeparam>
public abstract class Trick<TPlayer, TCard>
    where TPlayer : class
    where TCard : class
{
    private readonly ITrickRules<TCard> _rules;

    protected Trick(ITrickRules<TCard> rules)
    {
        _rules = rules;
    }

    public List<(TPlayer Player, TCard Card)> Plays { get; } = [];

    public bool IsEmpty => Plays.Count == 0;

    /// <summary>
    /// The card that fixed what has to be followed: the first one played that counts for the lead.
    /// <c>null</c> while the trick is empty, or holds nothing but tarot's Excuse.
    /// </summary>
    public TCard LeadCard
    {
        get
        {
            foreach (var (_, card) in Plays)
            {
                if (_rules.CountsForLead(card))
                {
                    return card;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// The suit that must be followed, or <c>null</c> when nothing has fixed one yet (or when the
    /// trick was led with a card that belongs to no suit, such as a tarot trump).
    /// </summary>
    public Suit? LeadSuit => LeadCard is null ? null : _rules.SuitOf(LeadCard);

    /// <summary>
    /// The strength of the strongest trump in the trick, or <c>null</c> if none has been played.
    /// </summary>
    public int? HighestTrumpStrength
    {
        get
        {
            int? highest = null;
            foreach (var (_, card) in Plays)
            {
                if (!_rules.IsTrump(card))
                {
                    continue;
                }

                var strength = _rules.Strength(card);
                if (highest is null || strength > highest)
                {
                    highest = strength;
                }
            }

            return highest;
        }
    }

    public void Add(TPlayer player, TCard card) => Plays.Add((player, card));

    /// <summary>
    /// The seat currently winning: the strongest trump if any was played, otherwise the strongest card
    /// of the lead suit. A card that belongs to no suit never wins on its own.
    /// </summary>
    public TPlayer CurrentWinner
    {
        get
        {
            if (Plays.Count == 0)
            {
                return null;
            }

            var trumpPlays = Plays.Where(play => _rules.IsTrump(play.Card)).ToList();
            if (trumpPlays.Count > 0)
            {
                return trumpPlays.MaxBy(play => _rules.Strength(play.Card)).Player;
            }

            var leadSuit = LeadSuit;
            if (leadSuit is null)
            {
                return Plays[0].Player;
            }

            return Plays
                .Where(play => _rules.SuitOf(play.Card) == leadSuit)
                .MaxBy(play => _rules.Strength(play.Card))
                .Player;
        }
    }

    public TPlayer DetermineWinner() => CurrentWinner;
}
