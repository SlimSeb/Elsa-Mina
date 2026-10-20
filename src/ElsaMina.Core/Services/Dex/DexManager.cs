using System.Text.Json;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Dex;

public class DexManager : IDexManager, IBotLifecycleParticipant
{
    private const string DEX_URL = "https://tyradex.app/api/v1/pokemon";

    private readonly IHttpService _httpService;

    public DexManager(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public Pokemon[] Pokedex { get; private set; } = [];
    public IReadOnlyDictionary<string, MoveData> Moves { get; private set; } = new Dictionary<string, MoveData>();
    public IReadOnlyDictionary<string, PokedexEntry> Species { get; private set; } = new Dictionary<string, PokedexEntry>();

    public async Task LoadDexAsync(CancellationToken cancellationToken = default)
    {
        // The Showdown data files ship with the bot: load them even when the remote dex is unreachable
        try
        {
            Moves = await ReadJsonFileAsync<Dictionary<string, MoveData>>("moves.json", cancellationToken);
            Species = await ReadJsonFileAsync<Dictionary<string, PokedexEntry>>("pokedex.json", cancellationToken);
            Log.Information("Dex: loaded {0} moves and {1} species", Moves.Count, Species.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading Showdown dex data");
        }

        try
        {
            Pokedex = (await _httpService.SendAsync<Pokemon[]>(HttpRequest.Get(DEX_URL), cancellationToken)).Data;
            Log.Information("Dex: loaded {0} Pokémon entries", Pokedex.Length);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading Dex");
        }
    }

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private static async Task<T> ReadJsonFileAsync<T>(string filename, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(Path.Join(AppContext.BaseDirectory, "Services", "Dex", filename));
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(json, JSON_OPTIONS);
    }

    public Task OnStartingAsync(CancellationToken cancellationToken) => LoadDexAsync(cancellationToken);
}
