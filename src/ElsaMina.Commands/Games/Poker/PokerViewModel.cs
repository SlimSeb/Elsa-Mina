using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Poker;

public class PokerViewModel : LocalizableViewModel
{
    public string BotName { get; init; }
    public string Trigger { get; init; }
    public string RoomId { get; init; }
    public IPokerGame Game { get; init; }

    /// <summary>
    /// The player the (private) hand panel is rendered for. <c>null</c> for the public panels.
    /// </summary>
    public PokerPlayer Viewer { get; init; }
}
