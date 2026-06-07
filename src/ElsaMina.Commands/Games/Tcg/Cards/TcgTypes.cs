namespace ElsaMina.Commands.Games.Tcg.Cards;

/// <summary>
/// Helpers for working with <see cref="TcgType"/> values: parsing, localization keys and the set of
/// types that may be chosen as a deck's Energy Zone types.
/// </summary>
public static class TcgTypes
{
    /// <summary>
    /// Types that can be selected as a deck's Energy Zone types. <see cref="TcgType.Colorless"/> is
    /// excluded: it only ever appears in attack costs, never as a generated energy.
    /// </summary>
    public static readonly IReadOnlyList<TcgType> SelectableEnergyTypes =
    [
        TcgType.Grass, TcgType.Fire, TcgType.Water,
        TcgType.Lightning, TcgType.Psychic, TcgType.Fighting
    ];

    public static bool TryParse(string value, out TcgType type) =>
        Enum.TryParse(value?.Trim(), ignoreCase: true, out type) && Enum.IsDefined(type);

    /// <summary>
    /// Localization key for a type's display name, e.g. <c>tcg_type_fire</c>.
    /// </summary>
    public static string NameKey(this TcgType type) => $"tcg_type_{type.ToString().ToLowerInvariant()}";
}
