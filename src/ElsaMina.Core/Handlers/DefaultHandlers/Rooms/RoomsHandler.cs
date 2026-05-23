using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;

namespace ElsaMina.Core.Handlers.DefaultHandlers.Rooms;

public sealed class RoomsHandler : Handler
{
    private const string CHAT_MESSAGE_MARKER = "c:";
    private const string DE_INIT_MARKER = "deinit";
    private const string JOIN_MARKER = "J";
    private const string LEAVE_MARKER = "L";
    private const string RENAME_MARKER = "N";
    private const string NO_INIT_MARKER = "noinit";

    private readonly IRoomsManager _roomsManager;
    private readonly IUserSaveQueue _userSaveQueue;

    public RoomsHandler(IRoomsManager roomsManager, IUserSaveQueue userSaveQueue)
    {
        _roomsManager = roomsManager;
        _userSaveQueue = userSaveQueue;
    }

    public override IReadOnlySet<string> HandledMessageTypes => (HashSet<string>)
    [
        CHAT_MESSAGE_MARKER, DE_INIT_MARKER, JOIN_MARKER, LEAVE_MARKER, RENAME_MARKER, NO_INIT_MARKER
    ];

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length < 2)
        {
            return Task.CompletedTask;
        }

        switch (parts[1])
        {
            case CHAT_MESSAGE_MARKER:
                if (parts.Length < 5)
                {
                    break;
                }

                var room = _roomsManager.GetRoom(roomId);
                if (!parts[4].StartsWith("/raw") && room != null)
                {
                    room.UpdateMessageQueue(parts[3], parts[4]);
                }

                _userSaveQueue.Enqueue(parts[3], roomId, UserAction.Chatting);

                break;
            case DE_INIT_MARKER:
                _roomsManager.RemoveRoom(roomId);
                break;
            case JOIN_MARKER:
                if (parts.Length < 3)
                {
                    break;
                }

                _roomsManager.AddUserToRoom(roomId, parts[2]);
                _userSaveQueue.Enqueue(parts[2], roomId, UserAction.Joining);
                break;
            case LEAVE_MARKER:
                if (parts.Length < 3)
                {
                    break;
                }

                _roomsManager.RemoveUserFromRoom(roomId, parts[2]);
                _userSaveQueue.Enqueue(parts[2], roomId, UserAction.Leaving);
                break;
            case RENAME_MARKER:
                _roomsManager.RenameUserInRoom(roomId, parts[3], parts[2]);
                break;
            case NO_INIT_MARKER:
                var errorMessage = parts[2] switch
                {
                    "joinfailed" => "Could not join room '{0}', probably due to a lack of permissions",
                    "nonexistent" => "Room '{0}' doesn't exist, please check configuration",
                    "namerequired" => "Could not join room '{0}' because the bot is not logged in",
                    _ => string.Empty
                };
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    Log.Error(errorMessage, roomId ?? "unknown");
                }

                break;
        }

        return Task.CompletedTask;
    }
}