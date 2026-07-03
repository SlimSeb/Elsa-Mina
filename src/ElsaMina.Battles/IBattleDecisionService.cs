namespace ElsaMina.Battles;

public interface IBattleDecisionService
{
    /// <summary>
    /// Computes the next decision for the given battle state.
    /// Returns null when no decision is required (battle over, waiting on the opponent, ...).
    /// </summary>
    Task<BattleDecision> GetDecisionAsync(BattleContext context, CancellationToken cancellationToken = default);
}
