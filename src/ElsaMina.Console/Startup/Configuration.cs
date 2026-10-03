using System.Text.Json.Serialization;
using ElsaMina.Cloud;
using ElsaMina.Cloud.S3;
using ElsaMina.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.DataAccess;
using ElsaMina.Logging;

namespace ElsaMina.Console.Startup;

/// <summary>
/// The settings read from config.json. Each project only sees the settings interface it defines.
/// </summary>
public class Configuration : ICommandsConfiguration, IDatabaseConfiguration, IS3CredentialsProvider,
    IGoogleServiceAccountConfiguration
{
    public LogLevel LogLevel { get; set; } = LogLevel.Info;
    public string Host { get; set; }
    [JsonConverter(typeof(NumberOrStringToStringConverter))]
    public string Port { get; set; }
    public string Name { get; set; }
    public string Password { get; set; }
    public string Trigger { get; set; }
    public IEnumerable<string> Rooms { get; set; }
    public IEnumerable<string> RoomBlacklist { get; set; }
    public IEnumerable<string> Whitelist { get; set; }
    public string Avatar { get; set; }
    public string DefaultRoom { get; set; }
    public string BugReportLink { get; set; } = string.Empty;
    public string GithubToken { get; set; } = string.Empty;
    public string GithubRepository { get; set; } = string.Empty;
    public string DefaultLocaleCode { get; set; }
    public string ConnectionString { get; set; }
    public int DatabaseMaxRetries { get; set; }
    public TimeSpan DatabaseRetryDelay { get; set; }
    public string YoutubeApiKey { get; set; }
    public string DictionaryApiKey { get; set; }
    public string RiotApiKey { get; set; }
    public string GeniusApiKey { get; set; }
    public string ArcadeWebhookUrl { get; set; }
    public string MistralApiKey { get; set; }
    public string ChatGptApiKey { get; set; }
    public string GeminiApiKey { get; set; }
    public string ElevenLabsApiKey { get; set; }
    public string KlipyApiKey { get; set; }
    public string UnsplashApiKey { get; set; }
    public string SpoonacularApiKey { get; set; }
    public string TwitchClientId { get; set; } = string.Empty;
    public string TwitchClientSecret { get; set; } = string.Empty;
    public string TwitterBearerToken { get; set; } = string.Empty;
    public TimeSpan PlayTimeUpdatesInterval { get; set; }
    public TimeSpan LoginRetryDelay { get; set; }
    public string ArcadeSpreadsheetName { get; set; }
    public string ArcadeHallOfFameSheetName { get; set; }
    public string CaaSpreadsheetName { get; set; }
    public string CaaSheetName { get; set; }
    public string DollsDriveName { get; set; }
    public IReadOnlyDictionary<string, IEnumerable<string>> EventAnnounces { get; set; }
    public IReadOnlyDictionary<string, string> DiscordWebhooks { get; set; } = new Dictionary<string, string>();
    public int UserUpdateBatchSize { get; set; }
    public TimeSpan UserUpdateFlushInterval { get; set; }
    public IReadOnlyDictionary<string, string> GoogleServiceAccountData { get; set; }
    public string S3BucketName { get; set; } = string.Empty;
    public string S3EndpointUrl { get; set; } = string.Empty;
    public string S3AccessKey { get; set; } = string.Empty;
    public string S3SecretKey { get; set; } = string.Empty;
    public string S3BaseUrl { get; set; } = string.Empty;
    public string LokiUrl { get; set; } = string.Empty;
    public string LokiUser { get; set; } = string.Empty;
    public string LokiApiKey { get; set; } = string.Empty;
    public string OtlpEndpoint { get; set; } = string.Empty;
    public string OltpHeaders { get; set; } = string.Empty;
}
