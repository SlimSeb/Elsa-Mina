namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// The kinds of misère (misery) a player may declare before playing their first card of the deal.
/// Each is a personal bonus paid by every other player to the declarer, independent of the contract.
/// </summary>
public enum TarotMisereType
{
    /// <summary>
    /// A hand holding no trump at all. The Excuse is not a trump, so it is tolerated.
    /// </summary>
    Trump,

    /// <summary>
    /// A hand holding no face card (Jack, Cavalier, Queen or King).
    /// </summary>
    Head
}
