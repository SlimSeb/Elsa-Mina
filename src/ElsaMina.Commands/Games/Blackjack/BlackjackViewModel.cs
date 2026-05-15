using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Blackjack;

public class BlackjackViewModel : LocalizableViewModel
{
    public BlackjackGame Game { get; init; }
    public required string BotName { get; init; }
    public required string Trigger { get; init; }
    public required string RoomId { get; init; }
}
