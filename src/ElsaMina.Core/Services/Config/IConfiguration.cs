using ElsaMina.FileSharing.S3;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Config;

public interface IConfiguration : IS3CredentialsProvider, ILoggingConfiguration
{
    string Host { get; }
    string Port { get; }
    string Name { get; }
    string Password { get; }
    string Trigger { get; }
    IEnumerable<string> Rooms { get; }
    IEnumerable<string> RoomBlacklist { get; }
    IEnumerable<string> Whitelist { get; }
    string Avatar { get; }
    string DefaultRoom { get; }
    string BugReportLink { get; }
    string GithubToken { get; }
    string GithubRepository { get; }
    string DefaultLocaleCode { get; }
    string ConnectionString { get; }
    int DatabaseMaxRetries { get; }
    TimeSpan DatabaseRetryDelay { get; }
    string YoutubeApiKey { get; }
    string DictionaryApiKey { get; }
    string RiotApiKey { get; }
    string GeniusApiKey { get; }
    string ArcadeWebhookUrl { get; }
    string MistralApiKey { get; }
    string ChatGptApiKey { get; }
    string GeminiApiKey { get; }
    string ElevenLabsApiKey { get; }
    string TenorApiKey { get; }
    string UnsplashApiKey { get; }
    string SpoonacularApiKey { get; }
    TimeSpan PlayTimeUpdatesInterval { get; }
    TimeSpan LoginRetryDelay { get; }
    string ArcadeSpreadsheetName { get; }
    string ArcadeHallOfFameSheetName { get; }
    string CaaSpreadsheetName { get; }
    string CaaSheetName { get; }
    IReadOnlyDictionary<string, IEnumerable<string>> EventAnnounces { get; }
    IReadOnlyDictionary<string, string> DiscordWebhooks { get; }
    int UserUpdateBatchSize { get; set; }
    TimeSpan UserUpdateFlushInterval { get; set; }
    IReadOnlyDictionary<string, string> GoogleServiceAccountData { get; set; }
    string OtlpEndpoint { get; }
    string OltpHeaders { get; }
}
