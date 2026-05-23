using ElsaMina.Core.Services.Config;
using Newtonsoft.Json;

namespace ElsaMina.Gui.Services.BotConfiguration;

public class ConfigurationFileService : IConfigurationFileService
{
    public string ConfigFilePath { get; } = Path.Combine(AppContext.BaseDirectory, "config.json");

    public bool ConfigExists => File.Exists(ConfigFilePath);

    public async Task<Configuration> LoadConfigurationAsync()
    {
        if (!ConfigExists)
        {
            return new Configuration
            {
                Host = "sim.smogon.com",
                Port = "8000",
                Trigger = "-",
                DefaultLocaleCode = "en-US",
                DatabaseMaxRetries = 3,
                DatabaseRetryDelay = TimeSpan.FromSeconds(5),
                LoginRetryDelay = TimeSpan.FromSeconds(5),
                PlayTimeUpdatesInterval = TimeSpan.FromMinutes(1),
                UserUpdateBatchSize = 100,
                UserUpdateFlushInterval = TimeSpan.FromSeconds(30)
            };
        }

        using var reader = new StreamReader(ConfigFilePath);
        var json = await reader.ReadToEndAsync();
        return JsonConvert.DeserializeObject<Configuration>(json) ?? new Configuration();
    }

    public async Task SaveConfigurationAsync(Configuration configuration)
    {
        var json = JsonConvert.SerializeObject(configuration, Formatting.Indented);
        await File.WriteAllTextAsync(ConfigFilePath, json);
    }
}
