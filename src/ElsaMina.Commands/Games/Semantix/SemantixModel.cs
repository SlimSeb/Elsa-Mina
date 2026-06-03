using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Semantix;

public class SemantixModel : LocalizableViewModel
{
    public required ISemantixGame CurrentGame { get; init; }
    public required string Trigger { get; init; }
    public required string BotName { get; init; }
    public required string RoomId { get; init; }
    public bool IsPrivateMode { get; init; }
    public bool ShowAnswer { get; init; }
}
