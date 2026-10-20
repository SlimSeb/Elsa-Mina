namespace ElsaMina.Core.Services.Dex;

public interface IDexManager
{
    Pokemon[] Pokedex { get; }
    IReadOnlyDictionary<string, MoveData> Moves { get; }

    /// <summary>
    /// Showdown's pokedex.json, keyed by species id.
    /// </summary>
    IReadOnlyDictionary<string, PokedexEntry> Species { get; }

    Task LoadDexAsync(CancellationToken cancellationToken = default);
}
