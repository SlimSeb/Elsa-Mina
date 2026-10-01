using Autofac;
using ElsaMina.Commands.Users;
using ElsaMina.Commands.Users.Colors;
using ElsaMina.Commands.Users.PlayTimes;
using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Commands.Users.Seen;
using ElsaMina.Commands.Users.Streaks;
using ElsaMina.Commands.Watchlist;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class UsersModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<StreaksRoomParameters>().As<IRoomParameterProvider>().SingleInstance();

        builder.RegisterCommand<NameColorInfoCommand>();
        builder.RegisterCommand<SetColorCommand>();
        builder.RegisterCommand<RemoveColorCommand>();
        builder.RegisterCommand<SeenCommand>();
        builder.RegisterCommand<AltsCommand>();
        builder.RegisterCommand<TopPlayTimesCommand>();
        builder.RegisterCommand<PlayTimeCommand>();
        builder.RegisterCommand<StreakCommand>();
        builder.RegisterCommand<StreakLeaderboardCommand>();
        builder.RegisterCommand<AddWatchlistCommand>();
        builder.RegisterCommand<RemoveWatchlistCommand>();

        builder.RegisterHandler<StreakUpdateHandler>();
        builder.RegisterHandler<UserActivityHandler>();
        builder.RegisterHandler<StaffIntroChangeHandler>();
        builder.RegisterHandler<StaffIntroContentHandler>();

        builder.RegisterType<NameColorsService>().As<INameColorsService>().As<IRoomColorsCache>().As<IBotLifecycleParticipant>().SingleInstance();
        builder.RegisterType<StreakService>().As<IStreakService>().SingleInstance();
        builder.RegisterType<CustomColorsManager>().As<ICustomColorsManager>().As<IBotLifecycleParticipant>()
            .SingleInstance();
        builder.RegisterType<UserColorsService>().As<IUserColorsService>().SingleInstance();
        builder.RegisterType<UserSaveQueue>().As<IUserSaveQueue>().As<IBotLifecycleParticipant>().SingleInstance();
        builder.RegisterType<PlayTimeUpdateService>().As<IPlayTimeUpdateService>().As<IBotLifecycleParticipant>()
            .SingleInstance();
        builder.RegisterType<RoomUserDataService>().As<IRoomUserDataService>().As<IBotLifecycleParticipant>()
            .SingleInstance();
        builder.RegisterType<WatchlistService>().As<IWatchlistService>().SingleInstance();
    }
}
