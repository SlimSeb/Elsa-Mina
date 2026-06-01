using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Wordle;

public class WordleModel : LocalizableViewModel
{
    public required IWordleGame CurrentGame { get; init; }
    public required string Trigger { get; init; }
    public required string BotName { get; init; }
    public required string RoomId { get; init; }
    public bool IsPrivateMode { get; init; }
}
