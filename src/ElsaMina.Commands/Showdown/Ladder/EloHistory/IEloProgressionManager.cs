namespace ElsaMina.Commands.Showdown.Ladder.EloHistory;

public interface IEloProgressionManager
{
    IReadOnlyCollection<EloTrackedUser> GetAllTrackedUsers();
    Task<bool> TrackUserAsync(string format, string userId, CancellationToken cancellationToken = default);
    Task<bool> UntrackUserAsync(string format, string userId, CancellationToken cancellationToken = default);
}
