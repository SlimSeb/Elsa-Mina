using ElsaMina.Commands.Ai.LanguageModel;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Ai.Chat;

public sealed class ConversationHistoryService : IConversationHistoryService
{
    private const int MAX_HISTORY_MESSAGES = 20;

    private readonly IConfiguration _configuration;

    public ConversationHistoryService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public List<LanguageModelMessage> BuildConversation(IRoom room, IUser sender, string latestMessage)
    {
        var botId = _configuration.Name.ToLowerAlphaNum();
        var conversation = new List<LanguageModelMessage>();

        foreach (var (user, message) in room.LastMessages.Reverse())
        {
            if (string.IsNullOrWhiteSpace(message) || IsCommandMessage(message))
            {
                continue;
            }

            var userId = user.ToLowerAlphaNum();
            var role = userId == botId ? MessageRole.Agent : MessageRole.User;
            conversation.Add(new LanguageModelMessage
            {
                Role = role,
                Content = role == MessageRole.User ? $"{FormatUserName(user)}: {message}" : message
            });
        }

        if (!string.IsNullOrWhiteSpace(latestMessage))
        {
            conversation.Add(new LanguageModelMessage
            {
                Role = MessageRole.User,
                Content = $"{FormatUserName(sender.Name)}: {latestMessage}"
            });
        }

        TrimHistory(conversation);
        return conversation;
    }

    private static void TrimHistory(List<LanguageModelMessage> history)
    {
        if (history.Count <= MAX_HISTORY_MESSAGES)
        {
            return;
        }

        var removeCount = history.Count - MAX_HISTORY_MESSAGES;
        history.RemoveRange(0, removeCount);
    }

    private static string FormatUserName(string user)
    {
        if (string.IsNullOrEmpty(user))
        {
            return user;
        }

        return user.Length > 1 && !char.IsLetterOrDigit(user[0]) ? user[1..] : user;
    }

    private bool IsCommandMessage(string message)
    {
        var trigger = _configuration.Trigger ?? string.Empty;
        return trigger.Length > 0 && message.StartsWith(trigger, StringComparison.OrdinalIgnoreCase);
    }
}
