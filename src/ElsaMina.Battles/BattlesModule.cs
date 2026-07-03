using Autofac;
using ElsaMina.Battles.Commands;
using ElsaMina.Battles.Strategies;
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
        builder.RegisterType<CalcBasedBattleDecisionService>().As<IBattleDecisionService>().SingleInstance();
        builder.RegisterType<BattleService>().As<IBattleService>().SingleInstance();
        builder.RegisterType<BattleTeamsService>().As<IBattleTeamsService>().SingleInstance();
        builder.RegisterType<LadderingService>().As<ILadderingService>().SingleInstance();

        builder.RegisterHandler<BattleHandler>();

        builder.RegisterCommand<SearchCommand>();
        builder.RegisterCommand<UseTeamCommand>();
        builder.RegisterCommand<LadderingCommand>();
    }
}
