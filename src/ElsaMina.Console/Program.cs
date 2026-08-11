using Autofac;
using ElsaMina.Battles;
using ElsaMina.Commands;
using ElsaMina.Console;
using ElsaMina.Core;
using ElsaMina.Core.Modules;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Telemetry;
using ElsaMina.FileSharing.S3;
using ElsaMina.Logging;
using Grafana.OpenTelemetry;
using Newtonsoft.Json;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Configuration
Configuration configuration;
using (var streamReader = new StreamReader("config.json"))
{
    var json = await streamReader.ReadToEndAsync();
    configuration = JsonConvert.DeserializeObject<Configuration>(json);
}

Log.Configuration = configuration;

// OpenTelemetry (instrumentation)
TracerProvider tracerProvider = null;
MeterProvider meterProvider = null;

var otlpEndpoint = configuration.OtlpEndpoint;
var oltpHeaders = configuration.OltpHeaders;

if (!string.IsNullOrWhiteSpace(otlpEndpoint) && !string.IsNullOrWhiteSpace(oltpHeaders))
{
    var exporter = new OtlpExporter
    {
        Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf,
        Endpoint = new Uri(configuration.OtlpEndpoint),
        Headers = configuration.OltpHeaders
    };
    tracerProvider = Sdk.CreateTracerProviderBuilder()
        .AddSource(TelemetryService.ACTIVITY_SOURCE_NAME)
        .UseGrafana(settings =>
        {
            settings.ServiceName = TelemetryService.SERVICE_NAME;
            settings.ExporterSettings = exporter;
        })
        .Build();

    meterProvider = Sdk.CreateMeterProviderBuilder()
        .AddMeter(TelemetryService.METER_NAME)
        .UseGrafana(settings =>
        {
            settings.ServiceName = TelemetryService.SERVICE_NAME;
            settings.ExporterSettings = exporter;
        })
        .Build();

    Log.Information("OpenTelemetry initialized - exporting to {0}", otlpEndpoint);
}
else
{
    Log.Warning("OpenTelemetry not initialized - OtlpEndpoint, OltpInstanceId or OltpAccessToken missing from config");
}

// DI
var builder = new ContainerBuilder();
builder.RegisterInstance(configuration).As<IConfiguration>().As<IS3CredentialsProvider>().SingleInstance();
builder.RegisterModule<CoreModule>();
builder.RegisterModule<BattlesModule>();
builder.RegisterModule<CommandModule>();
builder.RegisterType<VersionProvider>().As<IVersionProvider>();
var container = builder.Build();
var dependencyContainerService = container.Resolve<IDependencyContainerService>();
dependencyContainerService.SetContainer(container);
DependencyContainerService.Current = dependencyContainerService;

// Handle incoming messages, one at a time, in the order they arrive
var bot = dependencyContainerService.Resolve<IBot>();
var client = dependencyContainerService.Resolve<IClient>();

_ = Task.Run(async () =>
{
    await Parallel.ForEachAsync(
        client.Messages,
        async (message, _) => // mettre un token ?
        {
            try
            {
                await bot.HandleReceivedMessageAsync(message);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Error while handling message");
            }
        });
});

// Disconnect event
client.Disconnected += (_, info) =>
{
    Log.Warning(
        "Disconnected. Reason: {reason}, Status: {status}, Desc: {desc}, Exception: {ex}, Reconnecting: {reconnecting}",
        info.Reason,
        info.CloseStatus?.ToString() ?? string.Empty,
        info.CloseStatusDescription ?? string.Empty,
        info.Exception?.Message ?? string.Empty,
        info.WillReconnect
    );
    bot.OnDisconnect();
};

// Reconnection
client.Connected += (_, info) =>
{
    if (!info.IsReconnect)
    {
        return;
    }

    Log.Warning("Reconnected after {0} attempt(s), downtime : {1}", info.Attempt, info.Downtime);
    bot.OnReconnect();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    Log.Information("Exiting...");
    bot.OnExit();
    tracerProvider?.Dispose();
    meterProvider?.Dispose();
    Log.CloseAndFlush();
};

// Start
await bot.StartAsync();
var exitEvent = new ManualResetEvent(false);
exitEvent.WaitOne();
