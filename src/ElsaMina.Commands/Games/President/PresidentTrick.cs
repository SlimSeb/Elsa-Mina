namespace ElsaMina.Commands.Games.President;

/// <summary>
/// The pile currently being played on: an ordered list of plays, each being one or more cards of
/// the same rank. The first play fixes the number of cards every following play must contain.
/// </summary>
public sealed class PresidentTrick
{
    private readonly List<(PresidentPlayer Player, IReadOnlyList<PresidentCard> Cards)> _plays = [];

    public IReadOnlyList<(PresidentPlayer Player, IReadOnlyList<PresidentCard> Cards)> Plays => _plays;

    public bool IsEmpty => _plays.Count == 0;

    /// <summary>
    /// Number of cards every play on this pile must contain, or 0 while the pile is empty.
    /// </summary>
    public int RequiredCount => _plays.Count > 0 ? _plays[0].Cards.Count : 0;

    /// <summary>
    /// Rank of the current top play, which following plays must equal or beat.
    /// </summary>
    public int? TopRank => _plays.Count > 0 ? _plays[^1].Cards[0].Rank : null;

    public PresidentPlayer LastPlayer => _plays.Count > 0 ? _plays[^1].Player : null;

    public void Add(PresidentPlayer player, IReadOnlyList<PresidentCard> cards)
    {
        _plays.Add((player, cards));
    }
}
