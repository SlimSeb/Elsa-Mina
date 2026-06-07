namespace ElsaMina.Commands.Games.Tcg.Cards;

/// <summary>
/// A single, immutable card definition from <see cref="TcgCardPool"/>. Every card in this first
/// version is a Basic Pokémon. The card's <see cref="Id"/> is an independent identifier (a
/// collector number) decoupled from the Pokémon it depicts; the depicted Pokémon is given by
/// <see cref="Species"/>, which is the slug art is served from.
/// </summary>
public sealed record TcgCard
{
    /// <summary>
    /// Stable, unique identifier for this card (e.g. <c>"001"</c>). Independent of the depicted
    /// Pokémon, so several cards may share a <see cref="Species"/>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Lowercase Pokémon Showdown sprite slug for the depicted Pokémon (e.g. <c>"pikachu"</c>).
    /// </summary>
    public required string Species { get; init; }

    public required string Name { get; init; }

    public required int Hp { get; init; }

    public required TcgType Type { get; init; }

    /// <summary>
    /// The type this Pokémon is weak to (deals +20 when attacked by it), or <c>null</c> for none.
    /// </summary>
    public TcgType? Weakness { get; init; }

    /// <summary>
    /// Amount of energy that must be discarded to retreat this Pokémon.
    /// </summary>
    public int RetreatCost { get; init; }

    /// <summary>
    /// Whether this is a Pokémon ex (worth 2 points when knocked out instead of 1).
    /// </summary>
    public bool IsEx { get; init; }

    public required IReadOnlyList<TcgAttack> Attacks { get; init; }

    public string SpriteUrl => $"https://play.pokemonshowdown.com/sprites/gen5ani/{Species}.gif";
}
