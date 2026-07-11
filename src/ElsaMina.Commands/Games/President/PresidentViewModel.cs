using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.President;

public class PresidentViewModel : LocalizableViewModel
{
    public string BotName { get; init; }
    public string Trigger { get; init; }
    public string RoomId { get; init; }
    public IPresidentGame Game { get; init; }

    /// <summary>
    /// The player the (private) hand panel is rendered for. <c>null</c> for the public panels.
    /// </summary>
    public PresidentPlayer Viewer { get; init; }

    public IReadOnlyList<PresidentCard> ViewerHand { get; init; } = [];
    public IReadOnlyList<(int Rank, int Count)> ViewerLegalPlays { get; init; } = [];
    public bool ViewerCanPass { get; init; }
}
