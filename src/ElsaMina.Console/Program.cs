using ElsaMina.Console.Startup;
using ElsaMina.Core;
using ElsaMina.Logging;

var configuration = await ConfigurationLoader.LoadAsync();
Log.Configuration = configuration;

var telemetry = TelemetryBootstrapper.Initialize(configuration);

var dependencyContainerService = ContainerBootstrapper.Build(configuration);
var botHost = new BotHost(
    dependencyContainerService.Resolve<IBot>(),
    dependencyContainerService.Resolve<IClient>());
botHost.Start();

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    Log.Information("Exiting...");
    botHost.Shutdown();
    telemetry?.Dispose();
    Log.CloseAndFlush();
};

await botHost.RunAsync();
