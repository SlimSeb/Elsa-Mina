using Serilog.Core;
using Serilog.Events;

namespace ElsaMina.Logging;

public class GuiSink : ILogEventSink
{
    public static Action<LogLevel, string, DateTime>? OnLogEmitted { get; set; }

    public void Emit(LogEvent logEvent)
    {
        var callback = OnLogEmitted;
        if (callback == null)
        {
            return;
        }

        var level = logEvent.Level switch
        {
            LogEventLevel.Verbose => LogLevel.Verbose,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Info,
            LogEventLevel.Warning => LogLevel.Warning,
            _ => LogLevel.Error
        };

        callback(level, logEvent.RenderMessage(), logEvent.Timestamp.UtcDateTime);
    }
}
