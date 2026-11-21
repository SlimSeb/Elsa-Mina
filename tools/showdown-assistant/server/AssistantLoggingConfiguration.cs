using ElsaMina.Logging;
using LogLevel = ElsaMina.Logging.LogLevel;

namespace ElsaMina.BattleAssistant;

/// <summary>
/// What ElsaMina.Battles logs (model loaded, guard vetoes, search failures): information and above, to the console.
/// </summary>
public sealed class AssistantLoggingConfiguration : ILoggingConfiguration
{
    public LogLevel LogLevel => LogLevel.Info;
    public string LokiUrl { get; set; } = "";
    public string LokiUser { get; set; } = "";
    public string LokiApiKey { get; set; } = "";
}
