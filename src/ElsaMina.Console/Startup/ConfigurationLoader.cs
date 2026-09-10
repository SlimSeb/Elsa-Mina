using System.Text.Json;
using System.Text.Json.Serialization;
using ElsaMina.Core.Services.Config;

namespace ElsaMina.Console.Startup;

public static class ConfigurationLoader
{
    private const string CONFIG_FILE_NAME = "config.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(),
            new NumberOrStringToStringConverter()
        }
    };

    public static async Task<Configuration> LoadAsync()
    {
        using var streamReader = new StreamReader(CONFIG_FILE_NAME);
        var json = await streamReader.ReadToEndAsync();
        return JsonSerializer.Deserialize<Configuration>(json, Options);
    }
}
