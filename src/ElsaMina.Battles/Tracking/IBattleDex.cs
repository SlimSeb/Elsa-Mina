using ElsaMina.Core.Services.Dex;

namespace ElsaMina.Battles.Tracking;

/// <summary>
/// Gen 9 species and move data, looked up the way poke-env's GenData does.
/// </summary>
public interface IBattleDex
{
    /// <summary>
    /// Species by id, cosmetic formes included ("gastrodoneast" shares Gastrodon's entry).
    /// </summary>
    bool TryGetSpecies(string speciesId, out SpeciesEntry species);

    bool TryGetMove(string moveId, out MoveData move);
}
