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
            .UseGrafana(settings =>
            {
                settings.ServiceName = TelemetryService.SERVICE_NAME;
                settings.ExporterSettings = exporter;
            })
            .Build();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(TelemetryService.METER_NAME)
            .UseGrafana(settings =>
            {
                settings.ServiceName = TelemetryService.SERVICE_NAME;
                settings.ExporterSettings = exporter;
            })
            .Build();

        Log.Information("OpenTelemetry initialized - exporting to {0}", otlpEndpoint);

        return new TelemetryBootstrapper(tracerProvider, meterProvider);
    }

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _meterProvider?.Dispose();
    }
}
