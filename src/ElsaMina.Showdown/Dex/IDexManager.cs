namespace ElsaMina.Showdown.Dex;

public interface IDexManager
{
    Pokemon[] Pokedex { get; }
    IReadOnlyDictionary<string, MoveData> Moves { get; }
    Task LoadDexAsync(CancellationToken cancellationToken = default);
}