namespace ElsaMina.Commands.Showdown.Ladder.EloHistory;

public interface IEloProgressionManager
{
    IReadOnlyCollection<EloTrackedUser> GetAllTrackedUsers();
    bool TrackUser(string format, string userId);
    bool UntrackUser(string format, string userId);
}
