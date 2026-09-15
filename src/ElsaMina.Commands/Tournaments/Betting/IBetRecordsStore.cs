namespace ElsaMina.Commands.Tournaments.Betting;

public interface IBetRecordsStore
{
    Task SaveBetOutcomesAsync(string roomId, IEnumerable<string> bettorIds, IReadOnlySet<string> correctBettorIds,
        CancellationToken cancellationToken = default);
}
