using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Tcg.Cards;

public class TcgCardBrowserViewModel : LocalizableViewModel
{
    public string BotName { get; init; }
    public string Trigger { get; init; }

    public IReadOnlyList<TcgCard> Cards { get; init; }

    /// <summary>
    /// When set, the browser shows "+" buttons that add the card to this deck.
    /// </summary>
    public string TargetDeckName { get; init; }
}
