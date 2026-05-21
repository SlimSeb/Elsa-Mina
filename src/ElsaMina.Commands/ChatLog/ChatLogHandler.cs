using ElsaMina.Core.Handlers;

namespace ElsaMina.Commands.ChatLog;

public class ChatLogHandler : Handler
{
    private readonly IChatLogService _chatLogService;

    public ChatLogHandler(IChatLogService chatLogService)
    {
        _chatLogService = chatLogService;
    }

    public override IReadOnlySet<string> HandledMessageTypes => (HashSet<string>)["c:"];

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length < 5 || string.IsNullOrEmpty(roomId))
        {
            return Task.CompletedTask;
        }

        if (!long.TryParse(parts[2], out var unixTimestamp))
        {
            return Task.CompletedTask;
        }

        var username = parts[3].Trim();
        var message = parts[4];
        _chatLogService.Append(roomId, unixTimestamp, username, message);
        return Task.CompletedTask;
    }
}
