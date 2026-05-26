using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Core.Services.Rooms;

public interface IRoomsManager
{
    IEnumerable<IRoom> Rooms { get; }
    IRoom GetRoom(string roomId);
    bool HasRoom(string roomId);
    Task InitializeRoomAsync(string roomId, IEnumerable<string> lines, CancellationToken cancellationToken = default);
    void RemoveRoom(string roomId);
    void AddUserToRoom(string roomId, string username);
    void RemoveUserFromRoom(string roomId, string username);
    void RenameUserInRoom(string roomId, string formerName, string newName);
    void Clear();
}
