using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.RockPaperScissors;

public class RpsViewModel : LocalizableViewModel
{
    public string BotName { get; init; }
    public string Trigger { get; init; }
    public string RoomId { get; init; }
    public IReadOnlyList<string> Players { get; init; }
    public IReadOnlyDictionary<string, RpsChoice> Choices { get; init; }
    public int WaitingCount { get; init; }
}
