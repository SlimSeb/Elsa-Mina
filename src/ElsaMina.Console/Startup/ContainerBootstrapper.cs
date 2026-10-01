using Autofac;
using ElsaMina.Battles;
using ElsaMina.Commands;
using ElsaMina.Core.Modules;
using ElsaMina.Core.Services.Config;
using ElsaMina.Cloud.S3;

namespace ElsaMina.Console.Startup;

public static class ContainerBootstrapper
{
    public static IContainer Build(Configuration configuration)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(configuration).As<IConfiguration>().As<IS3CredentialsProvider>().SingleInstance();
        builder.RegisterModule<CoreModule>();
        builder.RegisterModule<BattlesModule>();
        builder.RegisterModule<CommandModule>();
        builder.RegisterType<VersionProvider>().As<IVersionProvider>();
        return builder.Build();
    }
}
