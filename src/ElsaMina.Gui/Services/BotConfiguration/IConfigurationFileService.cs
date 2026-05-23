using ElsaMina.Core.Services.Config;

namespace ElsaMina.Gui.Services.BotConfiguration;

public interface IConfigurationFileService
{
    bool ConfigExists { get; }
    string ConfigFilePath { get; }
    Task<Configuration> LoadConfigurationAsync();
    Task SaveConfigurationAsync(Configuration configuration);
}
