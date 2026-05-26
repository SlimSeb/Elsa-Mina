using System.Collections.Concurrent;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Rooms;

public class RoomsManager : IRoomsManager
{
    private readonly IRoomFactory _roomFactory;
    private readonly ConcurrentDictionary<string, IRoom> _rooms = new();

    public RoomsManager(IRoomFactory roomFactory)
    {
        _roomFactory = roomFactory;
    }

    public IEnumerable<IRoom> Rooms => _rooms.Values;

    public IRoom GetRoom(string roomId) =>
        _rooms.TryGetValue(roomId, out var room) ? room : null;

    public bool HasRoom(string roomId) => _rooms.ContainsKey(roomId);

    public async Task InitializeRoomAsync(string roomId, IEnumerable<string> lines,
        CancellationToken cancellationToken = default)
    {
        var room = await _roomFactory.CreateRoomAsync(roomId, lines.ToArray(), cancellationToken);
        _rooms[room.RoomId] = room;
    }

    public void RemoveRoom(string roomId)
    {
        Log.Information("Removing room {0}", roomId);
        _rooms.Remove(roomId, out _);
    }

    public void AddUserToRoom(string roomId, string username) =>
        GetRoom(roomId)?.AddUser(username);

    public void RemoveUserFromRoom(string roomId, string username) =>
        GetRoom(roomId)?.RemoveUser(username);

    public void RenameUserInRoom(string roomId, string formerName, string newName) =>
        GetRoom(roomId)?.RenameUser(formerName, newName);

    public void Clear() => _rooms.Clear();
}