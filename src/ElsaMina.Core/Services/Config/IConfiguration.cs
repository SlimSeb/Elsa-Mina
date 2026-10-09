using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Config;

/// <summary>
/// Les paramètres dont le runtime du bot a besoin : connexion, identité, rooms et accès. Les autres projets ont
/// leurs propres interfaces (bdd, cloud, features), toutes implémentées par la config chargée depuis config.json
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
