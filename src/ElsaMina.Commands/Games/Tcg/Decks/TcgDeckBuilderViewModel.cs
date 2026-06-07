using ElsaMina.Commands.Games.Tcg.Cards;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Tcg.Decks;

public class TcgDeckBuilderViewModel : LocalizableViewModel
{
    public string BotName { get; init; }
    public string Trigger { get; init; }

    public TcgDeck Deck { get; init; }
    public TcgDeckValidationResult Validation { get; init; }

    public IReadOnlyList<TcgCard> AllCards { get; init; }
    public IReadOnlyList<TcgType> SelectableEnergyTypes { get; init; }

    public int DeckSize => TcgDeckConstants.DECK_SIZE;
    public int MaxCopies => TcgDeckConstants.MAX_COPIES;
}
