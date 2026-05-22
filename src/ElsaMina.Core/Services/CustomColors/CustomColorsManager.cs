using System.Text.RegularExpressions;
using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.CustomColors;

public partial class CustomColorsManager : ICustomColorsManager
{
    public const string CUSTOM_COLORS_JSON_URL = "https://play.pokemonshowdown.com/config/colors.json";
    public const string CUSTOM_COLORS_JS_URL = "https://play.pokemonshowdown.com/config/config.js";

    private static readonly Regex BLOCK_REGEX = BlockRegex();

    private static readonly Regex ENTRY_REGEX = EntryRegex();

    private readonly IHttpService _httpService;

    public CustomColorsManager(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public IReadOnlyDictionary<string, string> CustomColorsMapping { get; private set; }
        = new Dictionary<string, string>();

    public async Task FetchCustomColorsAsync(CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();
        await FetchFromJsonAsync(result, cancellationToken);
        await FetchFromJsAsync(result, cancellationToken);
        CustomColorsMapping = result;
        Log.Information("Fetched {0} custom colors", CustomColorsMapping.Count);
    }

    private async Task FetchFromJsonAsync(Dictionary<string, string> result, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpService.GetAsync<Dictionary<string, string>>(CUSTOM_COLORS_JSON_URL,
                cancellationToken: cancellationToken);
            foreach (var kvp in response.Data)
            {
                result.TryAdd(kvp.Key, kvp.Value);
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not fetch custom colors from JSON");
        }
    }

    private async Task FetchFromJsAsync(Dictionary<string, string> result, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpService.GetAsync<string>(CUSTOM_COLORS_JS_URL,
                isRaw: true, cancellationToken: cancellationToken);
            foreach (var kvp in ParseCustomColors(response.Data))
            {
                result.TryAdd(kvp.Key, kvp.Value);
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not fetch custom colors from JS config");
        }
    }

    private static Dictionary<string, string> ParseCustomColors(string js)
    {
        var blockMatch = BLOCK_REGEX.Match(js);
        if (!blockMatch.Success)
        {
            return new Dictionary<string, string>();
        }

        var block = blockMatch.Groups[1].Value;
        var result = new Dictionary<string, string>();
        foreach (Match entry in ENTRY_REGEX.Matches(block))
        {
            result.TryAdd(entry.Groups[1].Value, entry.Groups[2].Value);
        }
        return result;
    }

    [GeneratedRegex(@"'([^']+)'\s*:\s*'([^']*)'", RegexOptions.Compiled)]
    private static partial Regex EntryRegex();
    [GeneratedRegex(@"Config\.customcolors\s*=\s*\{(.+?)\};", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex BlockRegex();
}