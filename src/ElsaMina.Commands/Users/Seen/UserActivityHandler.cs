using ElsaMina.Core.Handlers;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Users.Seen;

/// <summary>
/// Records when and where users were last seen, for the seen command.
/// </summary>
public sealed class UserActivityHandler : Handler
{
    private const string CHAT_MESSAGE_MARKER = "c:";
    private const string JOIN_MARKER = "J";
    private const string LEAVE_MARKER = "L";

    private static readonly IReadOnlySet<string> HANDLED_MESSAGE_TYPES =
        new HashSet<string> { CHAT_MESSAGE_MARKER, JOIN_MARKER, LEAVE_MARKER };

    private readonly IUserSaveQueue _userSaveQueue;

    public UserActivityHandler(IUserSaveQueue userSaveQueue)
    {
        _userSaveQueue = userSaveQueue;
    }

    public override IReadOnlySet<string> HandledMessageTypes => HANDLED_MESSAGE_TYPES;

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length < 2)
        {
            return Task.CompletedTask;
        }

        switch (parts[1])
        {
            case CHAT_MESSAGE_MARKER when parts.Length >= 5:
                _userSaveQueue.Enqueue(parts[3], roomId, UserAction.Chatting);
                break;
            case JOIN_MARKER when parts.Length >= 3:
                _userSaveQueue.Enqueue(parts[2], roomId, UserAction.Joining);
                break;
            case LEAVE_MARKER when parts.Length >= 3:
                _userSaveQueue.Enqueue(parts[2], roomId, UserAction.Leaving);
                break;
        }

        return Task.CompletedTask;
    }
}
