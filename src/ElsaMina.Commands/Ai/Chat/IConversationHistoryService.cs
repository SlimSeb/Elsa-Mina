using System.Collections.Generic;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.LanguageModel;

namespace ElsaMina.Commands.Ai.Chat;

public interface IConversationHistoryService
{
    List<LanguageModelMessage> BuildConversation(IRoom room, IUser sender, string latestMessage);
}
