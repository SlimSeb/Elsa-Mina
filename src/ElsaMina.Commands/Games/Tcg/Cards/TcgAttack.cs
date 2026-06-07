namespace ElsaMina.Commands.Games.Tcg.Cards;

/// <summary>
/// An attack printed on a <see cref="TcgCard"/>.
/// </summary>
/// <param name="Name">Display name of the attack.</param>
/// <param name="Cost">
/// The energy required, one entry per energy symbol. A <see cref="TcgType.Colorless"/> entry can be
/// paid with any energy; a typed entry must be paid with that type (or, when attaching, Colorless is
/// resolved last). For example <c>[Fire, Colorless]</c> means "one Fire energy plus one of any type".
/// </param>
/// <param name="Damage">Base damage dealt to the defending Pokémon, before weakness.</param>
public sealed record TcgAttack(string Name, IReadOnlyList<TcgType> Cost, int Damage)
{
    /// <summary>
    /// Total number of energy this attack costs, regardless of type.
    /// </summary>
    public int EnergyCount => Cost.Count;
}
