using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;
using Newtonsoft.Json;

namespace ElsaMina.Core.Services.Dex;

public class DexManager : IDexManager
{
    private const string DEX_URL = "https://tyradex.app/api/v1/pokemon";
    
    private readonly IHttpService _httpService;

    public DexManager(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public Pokemon[] Pokedex { get; private set; } = [];
    public IReadOnlyDictionary<string, MoveData> Moves { get; private set; } = new Dictionary<string, MoveData>();
    // todo : abilities & items

    public async Task LoadDexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Pokedex = (await _httpService.SendAsync<Pokemon[]>(HttpRequest.Get(DEX_URL), cancellationToken)).Data;
            Moves = await ReadJsonFileAsync<Dictionary<string, MoveData>>("moves.json", cancellationToken);
            Log.Information("Dex: loaded {0} Pokémon entries and {1} moves", Pokedex.Length, Moves.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading Dex");
        }
    }
    
    private static async Task<T> ReadJsonFileAsync<T>(string filename, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(Path.Join("Services", "Dex", filename));
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(cancellationToken);
        return JsonConvert.DeserializeObject<T>(json);
    }
}
