using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Config;

/// <summary>
/// Settings the bot runtime needs: connection, identity, rooms and access. Other projects define their own
/// settings interfaces (database, cloud, features), all implemented by the configuration
/// loaded from config.json.
/// </summary>
public interface IConfiguration : ILoggingConfiguration
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
    string MistralApiKey { get; }
    string ChatGptApiKey { get; }
    string GeminiApiKey { get; }
    TimeSpan LoginRetryDelay { get; }
}
