using Autofac;
using ElsaMina.Console.Startup;
using ElsaMina.Core;
using ElsaMina.Core.Services.Dispatch;
using ElsaMina.Logging;

var configuration = await ConfigurationLoader.LoadAsync();
Log.Configuration = configuration;

var telemetry = TelemetryBootstrapper.Initialize(configuration);

var container = ContainerBootstrapper.Build(configuration);
var botHost = new BotHost(
    container.Resolve<IBot>(),
    container.Resolve<IClient>(),
    container.Resolve<IIncomingMessageDispatcher>());
botHost.Start();

System.Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    _ = botHost.ShutdownAsync();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    Log.Information("Exiting...");
    // ProcessExit laisse quelques secondes max au handler : on bloque sur le flush au lieu de le lancer et return direct
    botHost.ShutdownAsync().GetAwaiter().GetResult();
    telemetry?.Dispose();
    Log.CloseAndFlush();
};

await botHost.RunAsync();
