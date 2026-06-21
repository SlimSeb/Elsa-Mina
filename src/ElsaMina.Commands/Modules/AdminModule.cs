using Autofac;
using ElsaMina.Commands.Development;
using ElsaMina.Commands.Development.Commands;
using ElsaMina.Commands.Development.HandlerDashboard;
using ElsaMina.Commands.Development.LagTest;
using ElsaMina.Commands.Misc.Help;
using ElsaMina.Commands.RoomDashboard;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class AdminModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

#if DEBUG
        builder.RegisterCommand<ScriptCommand>();
#endif
        builder.RegisterCommand<Ping>();
        builder.RegisterCommand<LagTestCommand>();
        builder.RegisterCommand<SetLocaleCommand>();
        builder.RegisterCommand<HelpCommand>();
        builder.RegisterCommand<CommandInfoCommand>();
        builder.RegisterCommand<ShowRoomDashboard>();
        builder.RegisterCommand<RoomConfigCommand>();
        builder.RegisterCommand<KillCommand>();
        builder.RegisterCommand<MaydayCommand>();
        builder.RegisterCommand<FeatureSwitchCommand>();
        builder.RegisterCommand<ToggleHandlerCommand>();
        builder.RegisterCommand<HandlerDashboardCommand>();
        builder.RegisterCommand<StopConnectionCommand>();
        builder.RegisterCommand<TemplatesDebugCommand>();
        builder.RegisterCommand<GetAllCommand>();
        builder.RegisterCommand<SayCommand>();
        builder.RegisterCommand<FailCommand>();
        builder.RegisterCommand<ChangelogCommand>();
        builder.RegisterCommand<MemoryUsageCommand>();
        builder.RegisterCommand<UptimeCommand>();
        builder.RegisterCommand<RunningCommands>();
        builder.RegisterCommand<CancelRunningCommand>();

        builder.RegisterHandler<LagTestHandler>();
        builder.RegisterHandler<JoinRoomOnInviteHandler>();
        builder.RegisterHandler<HelpHandler>();

        builder.RegisterType<LagTestManager>().As<ILagTestManager>().SingleInstance();
        builder.RegisterType<DataManager>().As<IDataManager>().SingleInstance().OnActivating(e =>
        {
            e.Instance.Initialize().Wait(); // Risque d'ANR mais obligé pour garantir la bonne initialisation...
        });
    }
}
