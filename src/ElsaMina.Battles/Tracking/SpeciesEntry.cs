namespace ElsaMina.Battles.Tracking;

/// <summary>
/// The pokedex data poke-env reads for a species (GenData.pokedex).
/// </summary>
/// <param name="BaseSpecies">Id of the base species ("ogerpon" for Ogerpon-Wellspring).</param>
/// <param name="Forme">Forme name ("Wellspring-Tera"), or null for a base species.</param>
/// <param name="Types">Type names as in the pokedex ("Fire"), converted when a Pokémon takes this form, like
/// poke-env (a few non-standard entries have types such as "Bird").</param>
/// <param name="BaseStats">Base stats keyed "hp", "atk", "def", "spa", "spd", "spe".</param>
/// <param name="Abilities">Ability names in pokedex order (slots "0", "1", "H", "S").</param>
public record SpeciesEntry(
    string BaseSpecies,
    string Forme,
    IReadOnlyList<string> Types,
    IReadOnlyDictionary<string, int> BaseStats,
    IReadOnlyList<string> Abilities);
