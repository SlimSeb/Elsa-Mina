using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Tarot;

public class TarotViewModel : LocalizableViewModel
{
    public string BotName { get; init; }
    public string Trigger { get; init; }
    public string RoomId { get; init; }
    public ITarotGame Game { get; init; }

    /// <summary>
    /// The player the (private) hand panel is rendered for. <c>null</c> for the public panels.
    /// </summary>
    public TarotPlayer Viewer { get; init; }

    public IReadOnlyList<TarotCard> ViewerHand { get; init; } = [];
    public IReadOnlyCollection<TarotCard> ViewerLegalMoves { get; init; } = [];
}
