using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.PrivateMessages;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Core.Contexts;

public class ContextFactory : IContextFactory
{
    private const string BOT_MESSAGE_PREFIX = "/botmsg ";

    private readonly IConfiguration _configuration;
    private readonly IResourcesService _resourcesService;
    private readonly IRoomsManager _roomsManager;
    private readonly IUserDetailsManager _userDetailsManager;
    private readonly IBot _bot;
    private readonly IPmSendersManager _pmSendersManager;

    // todo : refactor this shit~
    public ContextFactory(IConfiguration configuration,
        IResourcesService resourcesService,
        IRoomsManager roomsManager,
        IUserDetailsManager userDetailsManager,
        IBot bot,
        IPmSendersManager pmSendersManager)
    {
        _configuration = configuration;
        _resourcesService = resourcesService;
        _roomsManager = roomsManager;
        _userDetailsManager = userDetailsManager;
        _bot = bot;
        _pmSendersManager = pmSendersManager;
    }

    public IContext TryBuildContextFromReceivedMessage(string[] parts, string roomId = null)
    {
        switch (parts.Length)
        {
            case > 1 when parts[1] == "c:":
            {
                var room = _roomsManager.GetRoom(roomId);
                if (room == null)
                {
                    return null;
                }

                var timestamp = long.Parse(parts[2]);
                var userId = parts[3].ToLowerAlphaNum();
                var message = parts[4];
                var (target, command) = GetTargetAndCommand(message);
                var user = room.Users.GetValueOrDefault(userId);

                if (user == null)
                {
                    Log.Error("Error: User {UserId} not found in room {RoomId}", userId, roomId);
                    return null;
                }

                return new RoomContext(_configuration, _resourcesService, _roomsManager, _userDetailsManager,
                    _bot, message, target, user, command, room, timestamp)
                {
                    RawMessage = string.Join("|", parts)
                };
            }
            case > 2 when parts[1] == "pm":
            {
                var message = parts[4];
                if (message.StartsWith(BOT_MESSAGE_PREFIX))
                {
                    message = message[BOT_MESSAGE_PREFIX.Length..];
                }

                var (target, command) = GetTargetAndCommand(message);
                return new PmContext(_configuration, _resourcesService, _roomsManager, _userDetailsManager,
                    _bot, message, target, _pmSendersManager.GetUser(parts[2]), command)
                {
                    RawMessage = string.Join("|", parts)
                };
            }
            default:
                return null;
        }
    }

    private (string, string) GetTargetAndCommand(string message)
    {
        var trigger = _configuration.Trigger;
        return message.StartsWith(trigger)
            ? ParseMessage(message, trigger)
            : (null, null);
    }

    private static (string target, string command) ParseMessage(string message, string trigger)
    {
        var triggerLength = trigger.Length;
        if (message[..triggerLength] != trigger)
        {
            return (null, null);
        }

        var text = message[triggerLength..];
        var spaceIndex = text.IndexOf(' ');
        var command = spaceIndex > 0 ? text[..spaceIndex].ToLower() : text.Trim().ToLower();
        var target = spaceIndex > 0 ? text[(spaceIndex + 1)..] : string.Empty;
        return string.IsNullOrEmpty(command) ? (null, null) : (target, command);
    }
}