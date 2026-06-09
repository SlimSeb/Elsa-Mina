using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Belote;

public class BeloteViewModel : LocalizableViewModel
{
    public string BotName { get; init; }
    public string Trigger { get; init; }
    public string RoomId { get; init; }
    public IBeloteGame Game { get; init; }

    /// <summary>
    /// The player the (private) hand panel is rendered for. <c>null</c> for the public panels.
    /// </summary>
    public BelotePlayer Viewer { get; init; }

    public IReadOnlyList<BeloteCard> ViewerHand { get; init; } = [];
    public IReadOnlyCollection<BeloteCard> ViewerLegalMoves { get; init; } = [];
}
