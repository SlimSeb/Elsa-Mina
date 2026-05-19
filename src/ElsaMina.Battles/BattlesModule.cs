using Autofac;
using ElsaMina.Battles.Strategies;
using ElsaMina.Core.Utils;

namespace ElsaMina.Battles;

public class BattlesModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<BattleMessageParser>().As<IBattleMessageParser>().SingleInstance();
        builder.RegisterType<CalcBasedBattleDecisionService>().As<IBattleDecisionService>().SingleInstance();
        builder.RegisterType<BattleService>().As<IBattleService>().SingleInstance();

        builder.RegisterHandler<BattleHandler>();
    }
}
