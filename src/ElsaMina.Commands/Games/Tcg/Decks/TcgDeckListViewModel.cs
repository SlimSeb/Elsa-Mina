using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Tcg.Decks;

public class TcgDeckListViewModel : LocalizableViewModel
{
    public string BotName { get; init; }
    public string Trigger { get; init; }

    public IReadOnlyList<TcgDeckSummary> Decks { get; init; }
    public int MaxDecks => TcgDeckConstants.MAX_DECKS_PER_USER;
    public int DeckSize => TcgDeckConstants.DECK_SIZE;
}
