namespace ElsaMina.Battles;

public interface IBattleService
{
    Task HandleMessageAsync(string[] parts, string roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The battles the bot plays that are not over, by room id.
    /// </summary>
    IReadOnlyList<BattleContext> GetBattlesInProgress();
}
