using Autofac;
using ElsaMina.Battles.Commands;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.Llm;
using ElsaMina.Battles.Strategies.Prediction;
using ElsaMina.Battles.Strategies.Search;
using ElsaMina.Core.Utils;

namespace ElsaMina.Battles;

public class BattlesModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

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
}
