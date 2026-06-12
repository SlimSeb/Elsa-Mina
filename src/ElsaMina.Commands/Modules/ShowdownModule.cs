using Autofac;
using ElsaMina.Commands.Replays;
using ElsaMina.Commands.Showdown;
using ElsaMina.Commands.Showdown.BattleTracker;
using ElsaMina.Commands.Showdown.Ladder;
using ElsaMina.Commands.Showdown.Ladder.EloHistory;
using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.Commands.Showdown.SmogonStats;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class ShowdownModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterCommand<RankingCommand>();
        builder.RegisterCommand<SmogonStatsCommand>();
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
        builder.RegisterType<SmogonUsageDataProvider>().As<ISmogonUsageDataProvider>().SingleInstance();
        builder.RegisterType<LadderHistoryManager>().As<ILadderHistoryManager>().SingleInstance();
        builder.RegisterType<LadderTrackerManager>().As<ILadderTrackerManager>().SingleInstance();
        builder.RegisterType<EloProgressionManager>().As<IEloProgressionManager>().SingleInstance().OnActivating(e =>
        {
            e.Instance.InitializeAsync().Wait();
        }).AutoActivate();
        builder.RegisterType<EloHistoryService>().As<IEloHistoryService>().SingleInstance().OnActivating(e =>
        {
            e.Instance.Start();
        }).AutoActivate();
    }
}
