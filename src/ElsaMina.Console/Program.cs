using System.Net.WebSockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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

// Subscribe to message event
var bot = dependencyContainerService.Resolve<IBot>();
var client = dependencyContainerService.Resolve<IClient>();
client.MessageReceived
    .Select(message => bot.HandleReceivedMessageAsync(message).ToObservable())
    .Concat()
    .Catch((Exception exception) =>
    {
        Log.Error(exception, "Error while handling message");
        return Observable.Throw<Unit>(exception);
    })
    .Subscribe();

// Disconnect event
client.DisconnectionHappened.Subscribe(info =>
{
    Log.Warning(
        "Disconnected. Type: {type}, Status: {status}, Desc: {desc}, Exception: {ex}",
        info.Type,
        info.CloseStatus ?? WebSocketCloseStatus.Empty,
        info.CloseStatusDescription ?? string.Empty,
        info.Exception?.Message ?? string.Empty
    );
    bot.OnDisconnect();
});

// Reconnection
client.ReconnectionHappened.Subscribe(info =>
{
    Log.Warning("Reconnecting : {0}", info.Type);
    bot.OnReconnect();
});

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
