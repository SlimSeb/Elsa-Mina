using System.Diagnostics.Metrics;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Telemetry;
using ElsaMina.Logging;
using Grafana.OpenTelemetry;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ElsaMina.Console.Startup;

public sealed class TelemetryBootstrapper : IDisposable
{
    // The SDK preallocates one MetricPoint per allowed data point and per metric stream (2000 by default),
    // so the limits are sized to the real tag cardinality instead.
    private const int COMMAND_METRICS_CARDINALITY_LIMIT = 1000;
    private const int DEFAULT_CARDINALITY_LIMIT = 200;

    private static readonly Instrumentation[] ENABLED_INSTRUMENTATIONS =
    [
        Instrumentation.NetRuntime,
        Instrumentation.Process,
        Instrumentation.HttpClient
    ];

    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;

    private TelemetryBootstrapper(TracerProvider tracerProvider, MeterProvider meterProvider)
    {
        _tracerProvider = tracerProvider;
        _meterProvider = meterProvider;
    }

    public static TelemetryBootstrapper Initialize(IConfiguration configuration)
    {
        var otlpEndpoint = configuration.OtlpEndpoint;
        var otlpHeaders = configuration.OltpHeaders;

        if (string.IsNullOrWhiteSpace(otlpEndpoint) || string.IsNullOrWhiteSpace(otlpHeaders))
        {
            Log.Warning(
                "OpenTelemetry not initialized - OtlpEndpoint, OltpInstanceId or OltpAccessToken missing from config");
            return null;
        }

        var exporter = new OtlpExporter
        {
            Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf,
            Endpoint = new Uri(otlpEndpoint),
            Headers = otlpHeaders
        };

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(TelemetryService.ACTIVITY_SOURCE_NAME)
            .UseGrafana(settings => ConfigureGrafana(settings, exporter))
            .Build();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(TelemetryService.METER_NAME)
            .AddView(GetMetricStreamConfiguration)
            .UseGrafana(settings => ConfigureGrafana(settings, exporter))
            .Build();

        Log.Information("OpenTelemetry initialized - exporting to {0}", otlpEndpoint);

        return new TelemetryBootstrapper(tracerProvider, meterProvider);
    }

    private static void ConfigureGrafana(GrafanaOpenTelemetrySettings settings, ExporterSettings exporter)
    {
        settings.ServiceName = TelemetryService.SERVICE_NAME;
        settings.ExporterSettings = exporter;
        settings.Instrumentations.Clear();
        foreach (var instrumentation in ENABLED_INSTRUMENTATIONS)
        {
            settings.Instrumentations.Add(instrumentation);
        }
    }

    private static MetricStreamConfiguration GetMetricStreamConfiguration(Instrument instrument)
    {
        var isCommandMetric = instrument.Meter.Name == TelemetryService.METER_NAME
                              && instrument.Name.StartsWith("commands.", StringComparison.Ordinal);
        return new MetricStreamConfiguration
        {
            CardinalityLimit = isCommandMetric ? COMMAND_METRICS_CARDINALITY_LIMIT : DEFAULT_CARDINALITY_LIMIT
        };
    }

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _meterProvider?.Dispose();
    }
}
