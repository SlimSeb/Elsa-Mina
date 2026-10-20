using ElsaMina.Core.Services.Dex;

namespace ElsaMina.Battles.Tracking;

/// <summary>
/// Reads <see cref="IDexManager"/>'s Showdown data like poke-env's GenData.load_pokedex: cosmetic formes get their
/// base species' entry, and every entry knows its base species id.
/// </summary>
public class BattleDex : IBattleDex
{
    private const string PIKACHU = "pikachu";
    private const string PIKACHU_GMAX = "pikachugmax";
    private const string GMAX_SUFFIX = "gmax";

    private readonly IDexManager _dexManager;
    private readonly Lock _lock = new();
    private IReadOnlyDictionary<string, SpeciesEntry> _species;

    public BattleDex(IDexManager dexManager)
    {
        _dexManager = dexManager;
    }

    public bool TryGetSpecies(string speciesId, out SpeciesEntry species)
    {
        return GetSpecies().TryGetValue(speciesId, out species);
    }

    public bool TryGetMove(string moveId, out MoveData move)
    {
        return _dexManager.Moves.TryGetValue(moveId, out move);
    }

    private IReadOnlyDictionary<string, SpeciesEntry> GetSpecies()
    {
        if (_species != null)
        {
            return _species;
        }

        lock (_lock)
        {
            var pokedex = _dexManager.Species;
            if (_species != null || pokedex.Count == 0)
            {
                // Not loaded yet: don't cache an empty dex
                return _species ?? new Dictionary<string, SpeciesEntry>();
            }

            _species = BuildSpecies(pokedex);
            return _species;
        }
    }

    private static Dictionary<string, SpeciesEntry> BuildSpecies(IReadOnlyDictionary<string, PokedexEntry> pokedex)
    {
        // Cosmetic formes also have data-less entries (Gastrodon-East): the base species' entry replaces them below
        var species = pokedex
            .Where(pair => pair.Value.Types != null && pair.Value.BaseStats != null)
            .ToDictionary(pair => pair.Key, pair => ToSpeciesEntry(pair.Key, pair.Value));

        var otherFormes = new Dictionary<string, SpeciesEntry>();
        foreach (var (id, entry) in pokedex)
        {
            foreach (var cosmeticForme in entry.CosmeticFormes ?? [])
            {
                otherFormes[ShowdownId.ToId(cosmeticForme)] = species[id];
            }
        }

        if (species.TryGetValue(PIKACHU_GMAX, out var pikachuGmax))
        {
            foreach (var id in species.Keys.Where(id =>
                         id.StartsWith(PIKACHU, StringComparison.Ordinal) && id is not (PIKACHU or PIKACHU_GMAX)))
            {
                otherFormes[id + GMAX_SUFFIX] = pikachuGmax;
            }
        }

        foreach (var (id, entry) in otherFormes)
        {
            species[id] = entry;
        }

        return species;
    }

    private static SpeciesEntry ToSpeciesEntry(string id, PokedexEntry entry)
    {
        var baseStats = new Dictionary<string, int>
        {
            ["hp"] = entry.BaseStats.Hp,
            ["atk"] = entry.BaseStats.Atk,
            ["def"] = entry.BaseStats.Def,
            ["spa"] = entry.BaseStats.Spa,
            ["spd"] = entry.BaseStats.Spd,
            ["spe"] = entry.BaseStats.Spe
        };

        return new SpeciesEntry(
            ShowdownId.ToId(entry.BaseSpecies ?? id),
            entry.Forme,
            entry.Types,
            baseStats,
            entry.Abilities?.Values.ToList() ?? []);
    }
}
