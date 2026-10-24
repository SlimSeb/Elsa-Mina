using Autofac;
using ElsaMina.Battles.Commands;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.Llm;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeV1;
using ElsaMina.Battles.Strategies.MiniPokeV2;
using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Battles.Tracking;
using ElsaMina.Core.Services.Probabilities;
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
        // Swap MinimaxSearch for MonteCarloTreeSearch to use MCTS instead
        builder.RegisterType<MinimaxSearch>().As<IBattleSearchAlgorithm>().SingleInstance();
        builder.RegisterType<CalcBasedBattleDecisionService>().AsSelf().SingleInstance();
        builder.RegisterType<RandomBattleDecisionService>().AsSelf().SingleInstance();
        builder.RegisterType<TypeMatchupBattleDecisionService>().AsSelf().SingleInstance();
        builder.RegisterType<LlmBattlePromptBuilder>().As<ILlmBattlePromptBuilder>().SingleInstance();
        builder.RegisterType<LlmBattleDecisionParser>().As<ILlmBattleDecisionParser>().SingleInstance();
        builder.RegisterType<LlmBattleDecisionService>().AsSelf().SingleInstance();
        // minipoke models: one line each (see tools/minipoke/README.md)
        RegisterMiniPoke<MiniPokeV1Encoder>(builder, BattleDecisionStrategy.MiniPokeV1_1M);
        RegisterMiniPoke<MiniPokeV1Encoder>(builder, BattleDecisionStrategy.MiniPokeV1_4M);
        RegisterMiniPoke<MiniPokeV2Encoder>(builder, BattleDecisionStrategy.MiniPokeV2_1M);
        builder.RegisterType<BattleDecisionManager>().As<IBattleDecisionService>().As<IBattleDecisionManager>().SingleInstance();
        builder.RegisterType<BattleService>().As<IBattleService>().SingleInstance();
        builder.RegisterType<BattleTeamsService>().As<IBattleTeamsService>().SingleInstance();
        builder.RegisterType<LadderingService>().As<ILadderingService>().SingleInstance();

        builder.RegisterHandler<BattleHandler>();

        builder.RegisterCommand<SearchCommand>();
        builder.RegisterCommand<UseTeamCommand>();
        builder.RegisterCommand<LadderingCommand>();
        builder.RegisterCommand<BattleStrategyCommand>();
    }

    /// <summary>
    /// A minipoke model: the files exported to Models/{strategy}, played by its own decision service
    /// with the encoder matching the encoder.py it was trained with. The decision manager picks up every version.
    /// </summary>
    private static void RegisterMiniPoke<TEncoder>(ContainerBuilder builder, BattleDecisionStrategy strategy)
        where TEncoder : MiniPokeEncoder
    {
        var options = new MiniPokeModelOptions(strategy,
            Path.Join(AppContext.BaseDirectory, "Models", strategy.ToString()));
        builder.Register(_ => new MiniPokeOnnxModel(options)).Keyed<IMiniPokeModel>(strategy).SingleInstance();
        builder.RegisterType<TEncoder>()
            .WithParameter(TypedParameter.From(options))
            .Keyed<IMiniPokeEncoder>(strategy)
            .SingleInstance();
        builder.Register(context => new MiniPokeBattleDecisionService(
                context.ResolveKeyed<IMiniPokeModel>(strategy),
                context.ResolveKeyed<IMiniPokeEncoder>(strategy),
                context.Resolve<CalcBasedBattleDecisionService>(),
                context.Resolve<IRandomService>(),
                options))
            .AsSelf()
            .SingleInstance();
    }
}
