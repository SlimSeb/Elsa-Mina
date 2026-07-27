namespace ElsaMina.Core.Services.RoomInfo;

public interface IRoomInfoManager
{
    Task<RoomInfoDto> GetRoomInfoAsync(string roomId, CancellationToken cancellationToken = default);
    void HandleReceivedRoomInfo(string message);
}
