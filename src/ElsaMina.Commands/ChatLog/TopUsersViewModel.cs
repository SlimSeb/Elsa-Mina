using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.ChatLog;

public class TopUsersViewModel : LocalizableViewModel
{
    public required string RoomId { get; init; }
    public required IReadOnlyList<TopUsersRow> Users { get; init; }
    public required int MaxCount { get; init; }
}
