using Autofac;
using ElsaMina.Commands.Arcade.Caa;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Arcade.Inscriptions;
using ElsaMina.Commands.Arcade.Levels;
using ElsaMina.Commands.Arcade.Points;
using ElsaMina.Commands.Arcade.Sheets;
using ElsaMina.Commands.Arcade.Slots;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class ArcadeModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterCommand<SetArcadeLevelCommand>();
        builder.RegisterCommand<DisplayArcadeLevelsCommand>();
        builder.RegisterCommand<DeleteArcadeLevelCommand>();
        builder.RegisterCommand<GetArcadeLevelCommand>();
        builder.RegisterCommand<AddPointsCommand>();
        builder.RegisterCommand<RemovePointsCommand>();
        builder.RegisterCommand<LeaderboardCommand>();
        builder.RegisterCommand<ClearPointsCommand>();
        builder.RegisterCommand<ArcadeHallOfFameCommand>();
        builder.RegisterCommand<ArcadeSheetAddPointsCommand>();
        builder.RegisterCommand<AddCaaPointsCommand>();
        builder.RegisterCommand<CaaCommand>();
        builder.RegisterCommand<ArcadeInCommand>();
        builder.RegisterCommand<ArcadeLeaveCommand>();
        builder.RegisterCommand<ArcadeStartCommand>();
        builder.RegisterCommand<ArcadeStopCommand>();
        builder.RegisterCommand<ArcadeListCommand>();
        builder.RegisterCommand<ArcadeRemoveCommand>();
        builder.RegisterCommand<ArcadeTimerCommand>();
        builder.RegisterCommand<ArcadeAddCommand>();
        builder.RegisterCommand<SlotsFunCommand>();
        builder.RegisterCommand<ArcadePointsCommand>();
        builder.RegisterCommand<ConfigEventRolesCommand>();
        builder.RegisterCommand<SaveEventRoleCommand>();
        builder.RegisterCommand<DeleteEventRoleCommand>();
        builder.RegisterCommand<MuteGamesCommand>();
        builder.RegisterCommand<UnmuteGamesCommand>();

        builder.RegisterHandler<ArcadeEventsHandler>();

        builder.RegisterType<EventRoleMappingService>().As<IEventRoleMappingService>().SingleInstance();
        builder.RegisterType<ArcadeInscriptionsManager>().As<IArcadeInscriptionsManager>().SingleInstance();
        builder.RegisterType<ArcadeEventsService>().As<IArcadeEventsService>().SingleInstance();
    }
}
