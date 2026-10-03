using ElsaMina.Core.Services.Dex;

namespace ElsaMina.Commands.Games.GuessingGame.HigherLower;

/// <summary>
/// A comparable Pokémon attribute used by the <see cref="HigherLowerGame"/> (base stats, weight, dex number...).
/// </summary>
/// <param name="LabelKey">Localization key for the category's display name.</param>
/// <param name="ValueSelector">Extracts the numeric value to compare, or null when unavailable for a Pokémon.</param>
/// <param name="Formatter">Formats a value for display (including its unit).</param>
public record HigherLowerCategory(
    string LabelKey,
    Func<Pokemon, double?> ValueSelector,
    Func<double, string> Formatter);
