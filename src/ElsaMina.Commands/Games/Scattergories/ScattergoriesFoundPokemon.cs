namespace ElsaMina.Commands.Games.Scattergories;

/// <summary>
/// A Pokémon successfully named during a round, together with the player who claimed it first.
/// </summary>
public sealed record ScattergoriesFoundPokemon(string DisplayName, string SpriteUrl, string FinderName);
