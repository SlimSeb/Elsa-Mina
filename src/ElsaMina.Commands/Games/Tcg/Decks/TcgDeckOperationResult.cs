using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Tcg.Decks;

/// <summary>
/// Result of a deck-editing operation, carrying a localized message key and the affected deck (when
/// available) so callers can both report the outcome and re-render the builder panel.
/// </summary>
public sealed record TcgDeckOperationResult(bool Success, string MessageKey, object[] Args, TcgDeck Deck = null)
{
    public static TcgDeckOperationResult Ok(string messageKey, TcgDeck deck, params object[] args) =>
        new(true, messageKey, args, deck);

    public static TcgDeckOperationResult Fail(string messageKey, params object[] args) =>
        new(false, messageKey, args);
}
