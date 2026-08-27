namespace ElsaMina.Battles.Strategies;

public interface IBattleDecisionManager
{
    BattleDecisionStrategy ActiveStrategy { get; set; }
    bool TrySetStrategy(string strategyName, out BattleDecisionStrategy strategy);
    IReadOnlyList<BattleDecisionStrategy> AvailableStrategies { get; }
    IBattleDecisionService GetCurrentService();
}
