namespace ElsaMina.Battles;

public interface IBattleDecisionService
{
    bool TryGetDecision(BattleContext context, out BattleDecision decision);
}
