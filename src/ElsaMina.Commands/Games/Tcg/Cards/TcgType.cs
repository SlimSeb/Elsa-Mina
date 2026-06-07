namespace ElsaMina.Commands.Games.Tcg.Cards;

/// <summary>
/// The energy / Pokémon types used by the simplified TCG, mirroring TCG Pocket's set.
/// <see cref="Colorless"/> energy can pay for any energy slot in an attack cost.
/// </summary>
public enum TcgType
{
    Grass,
    Fire,
    Water,
    Lightning,
    Psychic,
    Fighting,
    Colorless
}
