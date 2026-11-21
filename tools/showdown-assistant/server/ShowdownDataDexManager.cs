using System.Text.Json;
using ElsaMina.Core.Services.Dex;

namespace ElsaMina.BattleAssistant;

/// <summary>
/// The Showdown data (moves.json, pokedex.json) Core ships next to the binaries, the only part of
/// <see cref="DexManager"/> the tracker and encoder read: without the remote Pokédex it also downloads.
/// </summary>
public sealed class ShowdownDataDexManager : IDexManager
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public Pokemon[] Pokedex => [];

    public IReadOnlyDictionary<string, MoveData> Moves { get; private set; } = new Dictionary<string, MoveData>();

    public IReadOnlyDictionary<string, PokedexEntry> Species { get; private set; } =
        new Dictionary<string, PokedexEntry>();

    public async Task LoadDexAsync(CancellationToken cancellationToken = default)
    {
        Moves = await ReadAsync<Dictionary<string, MoveData>>("moves.json", cancellationToken);
        Species = await ReadAsync<Dictionary<string, PokedexEntry>>("pokedex.json", cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Join(AppContext.BaseDirectory, "Services", "Dex", fileName);
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JSON_OPTIONS, cancellationToken)
               ?? throw new InvalidDataException($"{path} is empty");
    }
}
