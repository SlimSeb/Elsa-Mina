namespace ElsaMina.Commands.Showdown.BattleTracker;

public interface ILadderTrackerManager : IDisposable
{
    IReadOnlyCollection<LadderTracking> GetRoomTrackings(string roomId);
    IReadOnlyCollection<LadderTracking> GetAllTrackings();
    void StartTracking(string roomId, string format, string prefix);
    void StopTracking(string roomId, string format, string prefix);
}