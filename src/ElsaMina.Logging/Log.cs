using Serilog;
using Serilog.Core;
using Serilog.Enrichers.OpenTelemetry;
#if RELEASE
using Serilog.Sinks.Grafana.Loki;
#endif 

namespace ElsaMina.Logging;

public static class Log
{
    private static readonly Lazy<Logger> LOGGER = new(CreateLogger, isThreadSafe: true);

    private static Logger Instance => LOGGER.Value;

    public static ILoggingConfiguration? Configuration { get; set; }

    private static Logger CreateLogger()
    {
        var config = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithOpenTelemetryTraceId()
            .Enrich.WithOpenTelemetrySpanId()
            .MinimumLevel.Debug()
            .WriteTo.Console();

        switch (Configuration?.LogLevel)
        {
            case LogLevel.Verbose:
                config.MinimumLevel.Verbose();
                break;
            case LogLevel.Debug:
                config.MinimumLevel.Debug();
                break;
            case LogLevel.Info:
                config.MinimumLevel.Information();
                break;
            case LogLevel.Warning:
                config.MinimumLevel.Warning();
                break;
            default:
                config.MinimumLevel.Error();
                break;
        }
#if !DEBUG
        config.WriteTo.File("log.txt", rollingInterval: RollingInterval.Day);

        var lokiUrl = Configuration?.LokiUrl;
        var lokiUser = Configuration?.LokiUser;
        var lokiApiKey = Configuration?.LokiApiKey;

        if (!string.IsNullOrWhiteSpace(lokiUrl) &&
            !string.IsNullOrWhiteSpace(lokiUser) &&
            !string.IsNullOrWhiteSpace(lokiApiKey))
        {
            config.WriteTo.GrafanaLoki(
                lokiUrl,
                credentials: new LokiCredentials
                {
                    Login = lokiUser,
                    Password = lokiApiKey
                },
                labels:
                [
                    new LokiLabel { Key = "app", Value = "elsamina-core" },
                    new LokiLabel { Key = "env", Value = "prod" },
                    new LokiLabel { Key = "host", Value = Environment.MachineName }
                ]);
        }
#endif

        config.WriteTo.Sink(new ShowdownSink(), Serilog.Events.LogEventLevel.Information);

        return config.CreateLogger();
    }

    public static void Information(string messageTemplate, params object[] propertyValues) =>
        Instance.Information(messageTemplate, propertyValues);

    public static void Warning(string messageTemplate, params object[] propertyValues) =>
        Instance.Warning(messageTemplate, propertyValues);

    public static void Error(string messageTemplate, params object[] propertyValues) =>
        Instance.Error(messageTemplate, propertyValues);

    public static void Error(Exception ex, string messageTemplate, params object[] propertyValues) =>
        Instance.Error(ex, messageTemplate, propertyValues);

    public static void Debug(string messageTemplate, params object[] propertyValues) =>
        Instance.Debug(messageTemplate, propertyValues);

    public static void CloseAndFlush() => Serilog.Log.CloseAndFlush();
}
