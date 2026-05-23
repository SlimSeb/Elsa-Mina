using ElsaMina.Logging;

namespace ElsaMina.Gui.Pages.Dashboard;

public class LogEntryViewModel
{
    public LogLevel Level { get; }
    public string Message { get; }
    public DateTime Timestamp { get; }

    public string LevelText => Level switch
    {
        LogLevel.Verbose => "VRB",
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "???"
    };

    public string TimeText => Timestamp.ToLocalTime().ToString("HH:mm:ss");

    public string LevelColorHex => Level switch
    {
        LogLevel.Verbose => "#666666",
        LogLevel.Debug => "#999999",
        LogLevel.Info => "#E0E0E0",
        LogLevel.Warning => "#FFA726",
        LogLevel.Error => "#EF5350",
        _ => "#E0E0E0"
    };

    public LogEntryViewModel(LogLevel level, string message, DateTime timestamp)
    {
        Level = level;
        Message = message;
        Timestamp = timestamp;
    }
}
