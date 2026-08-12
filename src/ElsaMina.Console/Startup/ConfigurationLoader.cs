using ElsaMina.Core.Services.Config;
using Newtonsoft.Json;

namespace ElsaMina.Console.Startup;

public static class ConfigurationLoader
{
    private const string CONFIG_FILE_NAME = "config.json";

    public static async Task<Configuration> LoadAsync()
    {
        using var streamReader = new StreamReader(CONFIG_FILE_NAME);
        var json = await streamReader.ReadToEndAsync();
        return JsonConvert.DeserializeObject<Configuration>(json);
    }
}
