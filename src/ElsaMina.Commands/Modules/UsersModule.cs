using Autofac;
using ElsaMina.Commands.Users;
using ElsaMina.Commands.Users.Colors;
using ElsaMina.Commands.Users.PlayTimes;
using ElsaMina.Commands.Users.Streaks;
using ElsaMina.Commands.Watchlist;
using ElsaMina.Core.Services.CustomColors;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class UsersModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

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
        builder.RegisterHandler<StaffIntroChangeHandler>();
        builder.RegisterHandler<StaffIntroContentHandler>();

        builder.RegisterType<NameColorsService>().As<INameColorsService>().As<IRoomColorsCache>().SingleInstance();
        builder.RegisterType<StreakService>().As<IStreakService>().SingleInstance();
        builder.RegisterType<WatchlistService>().As<IWatchlistService>().SingleInstance();
    }
}
