using ElsaMina.Commands.Games.Tcg.Cards;
using ElsaMina.Commands.Games.Tcg.Decks;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.UnitTests.Commands.Games.Tcg;

public class TcgDeckValidatorTest
{
    /// <summary>
    /// Builds a legal 20-card deck: two copies each of the first ten cards in the pool, one energy type.
    /// </summary>
    private static TcgDeck LegalDeck()
    {
        var cards = TcgCardPool.AllCards
            .Take(10)
            .SelectMany(card => new[] { card.Id, card.Id })
            .ToList();

        return new TcgDeck
        {
            Id = "deck",
            OwnerId = "owner",
            Name = "legal",
            Cards = cards,
            EnergyTypes = [TcgType.Fire.ToString()]
        };
    }

    [Test]
    public void Test_Validate_ShouldSucceed_ForLegalDeck()
    {
        Assert.That(TcgDeckValidator.Validate(LegalDeck()).IsValid, Is.True);
    }

    [Test]
    public void Test_Validate_ShouldFail_WhenDeckIsNull()
    {
        var result = TcgDeckValidator.Validate(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorKey, Is.EqualTo("tcg_deck_invalid_missing"));
        }
    }

    [Test]
    public void Test_Validate_ShouldFail_WhenWrongCardCount()
    {
        var deck = LegalDeck();
        deck.Cards = deck.Cards.Take(19).ToList();

        var result = TcgDeckValidator.Validate(deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorKey, Is.EqualTo("tcg_deck_invalid_size"));
        }
    }

    [Test]
    public void Test_Validate_ShouldFail_WhenUnknownCard()
    {
        var deck = LegalDeck();
        var cards = deck.Cards;
        cards[0] = "not-a-real-card";
        deck.Cards = cards;

        var result = TcgDeckValidator.Validate(deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorKey, Is.EqualTo("tcg_deck_invalid_unknown_card"));
        }
    }

    [Test]
    public void Test_Validate_ShouldFail_WhenTooManyCopies()
    {
        // Three copies of one card, filling the rest with distinct singles to keep the count at 20.
        var firstId = TcgCardPool.AllCards[0].Id;
        var cards = new List<string> { firstId, firstId, firstId };
        cards.AddRange(TcgCardPool.AllCards.Skip(1).Take(17).Select(card => card.Id));

        var deck = LegalDeck();
        deck.Cards = cards;

        var result = TcgDeckValidator.Validate(deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cards, Has.Count.EqualTo(20));
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorKey, Is.EqualTo("tcg_deck_invalid_too_many_copies"));
        }
    }

    [Test]
    public void Test_Validate_ShouldFail_WhenNoEnergyTypes()
    {
        var deck = LegalDeck();
        deck.EnergyTypes = [];

        var result = TcgDeckValidator.Validate(deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorKey, Is.EqualTo("tcg_deck_invalid_energy_count"));
        }
    }

    [Test]
    public void Test_Validate_ShouldFail_WhenTooManyEnergyTypes()
    {
        var deck = LegalDeck();
        deck.EnergyTypes =
        [
            TcgType.Fire.ToString(), TcgType.Water.ToString(),
            TcgType.Grass.ToString(), TcgType.Psychic.ToString()
        ];

        var result = TcgDeckValidator.Validate(deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorKey, Is.EqualTo("tcg_deck_invalid_energy_count"));
        }
    }
}
