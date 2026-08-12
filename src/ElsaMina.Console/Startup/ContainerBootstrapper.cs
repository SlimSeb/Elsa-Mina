using Autofac;
using ElsaMina.Battles;
using ElsaMina.Commands;
using ElsaMina.Core.Modules;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.FileSharing.S3;

namespace ElsaMina.Console.Startup;

public static class ContainerBootstrapper
{
    public static IDependencyContainerService Build(Configuration configuration)
    {
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

        return dependencyContainerService;
    }
}
