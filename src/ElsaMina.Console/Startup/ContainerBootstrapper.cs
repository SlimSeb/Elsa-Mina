using Autofac;
using ElsaMina.Battles;
using ElsaMina.Cloud;
using ElsaMina.Cloud.S3;
using ElsaMina.Commands;
using ElsaMina.Core.Modules;
using ElsaMina.Core.Services.Config;
using ElsaMina.DataAccess;

namespace ElsaMina.Console.Startup;

public static class ContainerBootstrapper
{
    /// <param name="configuration">The settings read from config.json.</param>
    /// <param name="overrides">Registrations applied last, replacing earlier ones (used by tests to stub infrastructure).</param>
    public static IContainer Build(Configuration configuration, Action<ContainerBuilder> overrides = null)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(configuration)
            .As<IConfiguration>()
            .As<ICommandsConfiguration>()
            .As<IDatabaseConfiguration>()
            .As<IS3CredentialsProvider>()
            .As<IGoogleServiceAccountConfiguration>()
            .SingleInstance();
        builder.RegisterModule<DataAccessModule>();
        builder.RegisterModule<CloudModule>();
        builder.RegisterModule<CoreModule>();
        builder.RegisterModule<BattlesModule>();
        builder.RegisterModule<CommandModule>();
        builder.RegisterType<VersionProvider>().As<IVersionProvider>();
        overrides?.Invoke(builder);
        return builder.Build();
    }
}
