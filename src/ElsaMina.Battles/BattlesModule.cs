using Autofac;
using ElsaMina.Battles.Challenges;
using ElsaMina.Battles.Chat;
using ElsaMina.Battles.Commands;
using ElsaMina.Battles.Replays;
using ElsaMina.Battles.Results;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.Gladion;
using ElsaMina.Battles.Strategies.GladionSearch;
using ElsaMina.Battles.Strategies.GladionObservationV2;
using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Battles.Tournaments;
using ElsaMina.Battles.Tracking;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Utils;

namespace ElsaMina.Battles;

public class BattlesModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<BattleDex>().As<IBattleDex>().SingleInstance();
        builder.RegisterType<BattleMessageParser>().As<IBattleMessageParser>().SingleInstance();
        builder.RegisterType<SmogonOpponentMovesPredictor>().As<IOpponentMovesPredictor>().SingleInstance();
        builder.RegisterType<MinimaxSearch>().As<IBattleSearchAlgorithm>().SingleInstance();
        builder.RegisterType<CalcBasedBattleDecisionService>().AsSelf().SingleInstance();
        builder.RegisterType<GladionActionGuard>().AsSelf().SingleInstance();
        builder.RegisterType<ShowdownSimulator>().As<IShowdownSimulator>().SingleInstance();
        builder.RegisterType<GladionTurnSearch>().AsSelf().SingleInstance();
        // Gladion models: one line each (see tools/gladion/README.md)
        RegisterGladion<GladionEncoderV2>(builder, BattleDecisionStrategy.GladionV1_22M);
        builder.RegisterType<BattleDecisionManager>().As<IBattleDecisionService>().As<IBattleDecisionManager>().SingleInstance();
        builder.RegisterType<BattleService>().As<IBattleService>().SingleInstance();
        builder.RegisterType<BattleTeamsService>().As<IBattleTeamsService>().SingleInstance();
        builder.RegisterType<BattleTeamSender>().As<IBattleTeamSender>().SingleInstance();
        builder.RegisterType<TeamValidator>().As<ITeamValidator>().SingleInstance();
        builder.RegisterType<ChallengeService>().As<IChallengeService>().SingleInstance();
        builder.RegisterType<LadderingService>().As<ILadderingService>().SingleInstance();
        builder.RegisterType<LadderRatingService>().As<ILadderRatingService>().SingleInstance();
        builder.RegisterType<BattleResultsService>().As<IBattleResultsService>().SingleInstance();
        builder.RegisterType<ReplaySavingService>().As<IReplaySavingService>().SingleInstance();
        builder.RegisterType<BattleChatService>().As<IBattleChatService>().SingleInstance();
        builder.RegisterType<TournamentPlayingService>().As<ITournamentPlayingService>().SingleInstance();
        builder.RegisterType<TournamentsRoomParameters>().As<IRoomParameterProvider>().SingleInstance();

        builder.RegisterHandler<BattleHandler>();
        builder.RegisterHandler<UpdateSearchHandler>();
        builder.RegisterHandler<ReplayUploadedHandler>();
        builder.RegisterHandler<ChallengeHandler>();
        builder.RegisterHandler<TeamValidationPopupHandler>();
        builder.RegisterHandler<TournamentHandler>();

        builder.RegisterCommand<SearchCommand>();
        builder.RegisterCommand<UseTeamCommand>();
        builder.RegisterCommand<LadderingCommand>();
        builder.RegisterCommand<BattleStrategyCommand>();
        builder.RegisterCommand<SaveReplaysCommand>();
        builder.RegisterCommand<BattleStatusCommand>();
        builder.RegisterCommand<JoinTournamentCommand>();
        builder.RegisterCommand<LeaveTournamentCommand>();
    }

    /// <summary>
    /// A Gladion model: the files exported to Models/{strategy}, played by its own decision service
    /// with the encoder matching the encoder.py it was trained with. The decision manager picks up every version.
    /// </summary>
    private static void RegisterGladion<TEncoder>(ContainerBuilder builder, BattleDecisionStrategy strategy)
        where TEncoder : GladionEncoder
    {
        var options = new GladionModelOptions(strategy,
            Path.Join(AppContext.BaseDirectory, "Models", strategy.ToString()));
        builder.Register(_ => new GladionOnnxModel(options)).Keyed<IGladionModel>(strategy).SingleInstance();
        builder.RegisterType<TEncoder>()
            .WithParameter(TypedParameter.From(options))
            .Keyed<IGladionEncoder>(strategy)
            .SingleInstance();
        RegisterGladionService(builder, strategy, options);

        // Its search variant plays with the same model and encoder
        if (strategy.WithSearch() is { } searchStrategy)
        {
            RegisterGladionService(builder, strategy, options with { Strategy = searchStrategy, Search = true });
        }
    }

    private static void RegisterGladionService(ContainerBuilder builder, BattleDecisionStrategy modelStrategy,
        GladionModelOptions options)
    {
        builder.Register(context => new GladionBattleDecisionService(
                context.ResolveKeyed<IGladionModel>(modelStrategy),
                context.ResolveKeyed<IGladionEncoder>(modelStrategy),
                context.Resolve<CalcBasedBattleDecisionService>(),
                context.Resolve<GladionActionGuard>(),
                context.Resolve<GladionTurnSearch>(),
                options))
            .AsSelf()
            .SingleInstance();
    }
}
