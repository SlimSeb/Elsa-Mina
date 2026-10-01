using Autofac;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Showdown.Dex;
using ElsaMina.Showdown.Smogon;

namespace ElsaMina.Showdown;

/// <summary>
/// Pokémon data shared by the commands and the battle bot: the dex and Smogon usage statistics.
/// </summary>
public class ShowdownDataModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<DexManager>().As<IDexManager>().As<IBotLifecycleParticipant>().SingleInstance();
        builder.RegisterType<SmogonUsageDataProvider>().As<ISmogonUsageDataProvider>().SingleInstance();
    }
}
