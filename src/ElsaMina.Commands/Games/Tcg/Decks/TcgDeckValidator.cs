using ElsaMina.Commands.Games.Tcg.Cards;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Tcg.Decks;

/// <summary>
/// Validates a saved deck against the rules required to battle with it: a fixed deck size, a copy
/// limit per card, only known cards, and a valid number of Energy Zone types.
/// </summary>
public static class TcgDeckValidator
{
    public static TcgDeckValidationResult Validate(TcgDeck deck)
    {
        if (deck is null)
        {
            return TcgDeckValidationResult.Invalid("tcg_deck_invalid_missing");
        }

        var cards = deck.Cards;

        if (cards.Count != TcgDeckConstants.DECK_SIZE)
        {
            return TcgDeckValidationResult.Invalid("tcg_deck_invalid_size",
                cards.Count, TcgDeckConstants.DECK_SIZE);
        }

        var unknown = cards.FirstOrDefault(id => !TcgCardPool.TryGet(id, out _));
        if (unknown is not null)
        {
            return TcgDeckValidationResult.Invalid("tcg_deck_invalid_unknown_card", unknown);
        }

        var tooMany = cards
            .GroupBy(id => id)
            .FirstOrDefault(group => group.Count() > TcgDeckConstants.MAX_COPIES);
        if (tooMany is not null)
        {
            return TcgDeckValidationResult.Invalid("tcg_deck_invalid_too_many_copies",
                tooMany.Key, TcgDeckConstants.MAX_COPIES);
        }

        var energyTypes = deck.EnergyTypes;
        if (energyTypes.Count < TcgDeckConstants.MIN_ENERGY_TYPES
            || energyTypes.Count > TcgDeckConstants.MAX_ENERGY_TYPES)
        {
            return TcgDeckValidationResult.Invalid("tcg_deck_invalid_energy_count",
                TcgDeckConstants.MIN_ENERGY_TYPES, TcgDeckConstants.MAX_ENERGY_TYPES);
        }

        if (energyTypes.Any(name => !TcgTypes.TryParse(name, out _)))
        {
            return TcgDeckValidationResult.Invalid("tcg_deck_invalid_energy_count",
                TcgDeckConstants.MIN_ENERGY_TYPES, TcgDeckConstants.MAX_ENERGY_TYPES);
        }

        return TcgDeckValidationResult.Valid;
    }
}
