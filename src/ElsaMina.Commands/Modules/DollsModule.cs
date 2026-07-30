using Autofac;
using ElsaMina.Commands.Dolls;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class DollsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterCommand<GiveDollCommand>();
        builder.RegisterCommand<TakeDollCommand>();
        builder.RegisterCommand<DollListCommand>();
        builder.RegisterCommand<RefreshDollsCommand>();

        builder.RegisterType<DollService>().As<IDollService>().SingleInstance();
    }
}
