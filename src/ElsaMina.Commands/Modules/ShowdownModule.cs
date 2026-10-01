using Autofac;
using ElsaMina.Commands.Replays;
using ElsaMina.Commands.Showdown;
using ElsaMina.Commands.Showdown.BattleTracker;
using ElsaMina.Commands.Showdown.Ladder;
using ElsaMina.Commands.Showdown.Ladder.EloHistory;
using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.Commands.Showdown.SmogonStats;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class ShowdownModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<ReplaysRoomParameters>().As<IRoomParameterProvider>().SingleInstance();

        builder.RegisterCommand<RankingCommand>();
        builder.RegisterCommand<SmogonStatsCommand>();
        builder.RegisterCommand<UsageHistoryCommand>();
        builder.RegisterCommand<LadderCommand>();
        builder.RegisterCommand<ShowAvatarCommand>();
        builder.RegisterCommand<ToggleLadderTrackerCommand>();
        builder.RegisterCommand<CurrentLadderTrackersCommand>();
        builder.RegisterCommand<LadderGraphCommand>();
        builder.RegisterCommand<TrackEloProgressionCommand>();
        builder.RegisterCommand<UntrackEloProgressionCommand>();
        builder.RegisterCommand<ListTrackedEloProgressionsCommand>();

        builder.RegisterHandler<ReplaysHandler>();

        builder.RegisterType<ShowdownRanksProvider>().As<IShowdownRanksProvider>().SingleInstance();
        builder.RegisterType<BestRankingProvider>().As<IBestRankingProvider>().SingleInstance();
        builder.RegisterType<LadderHistoryManager>().As<ILadderHistoryManager>().SingleInstance();
        builder.RegisterType<LadderTrackerManager>().As<ILadderTrackerManager>().SingleInstance();
        builder.RegisterType<EloProgressionManager>().As<IEloProgressionManager>().As<IBotLifecycleParticipant>()
            .SingleInstance();
        builder.RegisterType<EloHistoryService>().As<IEloHistoryService>().As<IBotLifecycleParticipant>()
            .SingleInstance();
    }
}
