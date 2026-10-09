using ElsaMina.Core.Services.Config;

namespace ElsaMina.Commands;

/// <summary>
/// Les paramètres utilisés que par les features : clés d'API, noms de sheets, webhooks, timings...
/// Le <see cref="IConfiguration"/> de Core garde juste ce dont le runtime du bot a besoin
/// </summary>
public interface ICommandsConfiguration : IConfiguration
{
    string YoutubeApiKey { get; }
    string DictionaryApiKey { get; }
    string RiotApiKey { get; }
    string GeniusApiKey { get; }
    string ArcadeWebhookUrl { get; }
    string ElevenLabsApiKey { get; }
    string KlipyApiKey { get; }
    string UnsplashApiKey { get; }
    string SpoonacularApiKey { get; }
    string TwitchClientId { get; }
    string TwitchClientSecret { get; }
    string TwitterBearerToken { get; }
    string ArcadeSpreadsheetName { get; }
    string ArcadeHallOfFameSheetName { get; }
    string CaaSpreadsheetName { get; }
    string CaaSheetName { get; }
    string DollsDriveName { get; }
    IReadOnlyDictionary<string, IEnumerable<string>> EventAnnounces { get; }
    IReadOnlyDictionary<string, string> DiscordWebhooks { get; }
    TimeSpan PlayTimeUpdatesInterval { get; }
    int UserUpdateBatchSize { get; }
    TimeSpan UserUpdateFlushInterval { get; }
}
