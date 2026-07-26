using ElsaMina.Commands.Games.Cards;
using System.Globalization;
using ElsaMina.Commands.Games.Belote;
using ElsaMina.Commands.Games.Poker;
using ElsaMina.Commands.Games.President;
using ElsaMina.Commands.Games.Tarot;

namespace ElsaMina.IntegrationTests.Commands.Games.Rules;

/// <summary>
/// Pins the human-readable card labels the panels show. Suit symbols and the French face letters
/// (V/C/D/R for valet, cavalier, dame, roi) are visible to every player of every deal, so they are
/// treated as observable behaviour rather than formatting detail.
/// </summary>
[TestFixture]
public class CardDisplayTest
{
    private static readonly CultureInfo FRENCH = new("fr-FR");
    private static readonly CultureInfo ENGLISH = new("en-US");

    [TestCase(Suit.Hearts, TarotCard.JACK, "V♥", "J♥")]
    [TestCase(Suit.Hearts, TarotCard.CAVALIER, "C♥", "C♥")]
    [TestCase(Suit.Hearts, TarotCard.QUEEN, "D♥", "Q♥")]
    [TestCase(Suit.Hearts, TarotCard.KING, "R♥", "K♥")]
    [TestCase(Suit.Spades, TarotCard.KING, "R♠", "K♠")]
    [TestCase(Suit.Diamonds, TarotCard.QUEEN, "D♦", "Q♦")]
    [TestCase(Suit.Clubs, TarotCard.JACK, "V♣", "J♣")]
    [TestCase(Suit.Hearts, 1, "1♥", "1♥")]
    [TestCase(Suit.Spades, 10, "10♠", "10♠")]
    public void Test_TarotCard_ShouldDisplayTheExpectedLabel(Suit suit, int rank, string french,
        string english)
    {
        var card = TarotCard.Suited(suit, rank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.ToDisplay(FRENCH), Is.EqualTo(french));
            Assert.That(card.ToDisplay(ENGLISH), Is.EqualTo(english));
        }
    }

    /// <summary>
    /// Trumps are prefixed A for atout in French and T for trump in English.
    /// </summary>
    [TestCase(TarotCard.PETIT, "A1", "T1")]
    [TestCase(12, "A12", "T12")]
    [TestCase(TarotCard.MONDE, "A21", "T21")]
    public void Test_TarotTrump_ShouldDisplayTheExpectedLabel(int rank, string french, string english)
    {
        var card = TarotCard.Trump(rank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.ToDisplay(FRENCH), Is.EqualTo(french));
            Assert.That(card.ToDisplay(ENGLISH), Is.EqualTo(english));
        }
    }

    [Test]
    public void Test_TarotExcuse_ShouldDisplayAsAJokerInEveryLanguage()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TarotCard.Excuse.ToDisplay(FRENCH), Is.EqualTo("🃏"));
            Assert.That(TarotCard.Excuse.ToDisplay(ENGLISH), Is.EqualTo("🃏"));
        }
    }

    [TestCase(Suit.Hearts, BeloteCard.JACK, "V♥", "J♥")]
    [TestCase(Suit.Hearts, BeloteCard.QUEEN, "D♥", "Q♥")]
    [TestCase(Suit.Hearts, BeloteCard.KING, "R♥", "K♥")]
    [TestCase(Suit.Hearts, BeloteCard.ACE, "A♥", "A♥")]
    [TestCase(Suit.Spades, BeloteCard.KING, "R♠", "K♠")]
    [TestCase(Suit.Diamonds, 10, "10♦", "10♦")]
    [TestCase(Suit.Clubs, 7, "7♣", "7♣")]
    public void Test_BeloteCard_ShouldDisplayTheExpectedLabel(Suit suit, int rank, string french,
        string english)
    {
        var card = new BeloteCard(suit, rank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.ToDisplay(FRENCH), Is.EqualTo(french));
            Assert.That(card.ToDisplay(ENGLISH), Is.EqualTo(english));
        }
    }

    [TestCase(Suit.Hearts, PresidentCard.JACK, "V♥", "J♥")]
    [TestCase(Suit.Hearts, PresidentCard.QUEEN, "D♥", "Q♥")]
    [TestCase(Suit.Hearts, PresidentCard.KING, "R♥", "K♥")]
    [TestCase(Suit.Hearts, PresidentCard.ACE, "A♥", "A♥")]
    [TestCase(Suit.Hearts, PresidentCard.TWO, "2♥", "2♥")]
    [TestCase(Suit.Spades, 3, "3♠", "3♠")]
    [TestCase(Suit.Diamonds, 10, "10♦", "10♦")]
    [TestCase(Suit.Clubs, PresidentCard.KING, "R♣", "K♣")]
    public void Test_PresidentCard_ShouldDisplayTheExpectedLabel(Suit suit, int rank, string french,
        string english)
    {
        var card = new PresidentCard(suit, rank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.ToDisplay(FRENCH), Is.EqualTo(french));
            Assert.That(card.ToDisplay(ENGLISH), Is.EqualTo(english));
        }
    }

    /// <summary>
    /// Poker labels are culture-independent: they are the ones printed on an English deck.
    /// </summary>
    [TestCase(Suit.Hearts, PokerCard.JACK, "J♥")]
    [TestCase(Suit.Diamonds, PokerCard.QUEEN, "Q♦")]
    [TestCase(Suit.Spades, PokerCard.KING, "K♠")]
    [TestCase(Suit.Clubs, PokerCard.ACE, "A♣")]
    [TestCase(Suit.Hearts, 10, "10♥")]
    [TestCase(Suit.Spades, 2, "2♠")]
    public void Test_PokerCard_ShouldDisplayTheExpectedLabel(Suit suit, int rank, string expected)
    {
        Assert.That(new PokerCard(suit, rank).ToDisplay(), Is.EqualTo(expected));
    }

    [TestCase(Suit.Hearts, true)]
    [TestCase(Suit.Diamonds, true)]
    [TestCase(Suit.Spades, false)]
    [TestCase(Suit.Clubs, false)]
    public void Test_PokerCard_ShouldKnowWhichSuitsAreRed(Suit suit, bool expected)
    {
        Assert.That(new PokerCard(suit, PokerCard.ACE).IsRed, Is.EqualTo(expected));
    }

    /// <summary>
    /// With no culture the games fall back to the English labels.
    /// </summary>
    [Test]
    public void Test_Cards_ShouldFallBackToTheEnglishLabels_WhenNoCultureIsGiven()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TarotCard.Suited(Suit.Hearts, TarotCard.KING).ToDisplay(), Is.EqualTo("K♥"));
            Assert.That(TarotCard.Trump(5).ToDisplay(), Is.EqualTo("T5"));
            Assert.That(new BeloteCard(Suit.Spades, BeloteCard.JACK).ToDisplay(), Is.EqualTo("J♠"));
            Assert.That(new PresidentCard(Suit.Clubs, PresidentCard.QUEEN).ToDisplay(),
                Is.EqualTo("Q♣"));
        }
    }

    [TestCase(Suit.Hearts, "♥")]
    [TestCase(Suit.Spades, "♠")]
    [TestCase(Suit.Diamonds, "♦")]
    [TestCase(Suit.Clubs, "♣")]
    public void Test_BeloteSuitDisplay_ShouldUseTheExpectedSymbol(Suit suit, string expected)
    {
        Assert.That(CardToken.SuitSymbol(suit), Is.EqualTo(expected));
    }

    [TestCase(PresidentCard.JACK, "V", "J")]
    [TestCase(PresidentCard.QUEEN, "D", "Q")]
    [TestCase(PresidentCard.KING, "R", "K")]
    [TestCase(PresidentCard.ACE, "A", "A")]
    [TestCase(PresidentCard.TWO, "2", "2")]
    [TestCase(10, "10", "10")]
    public void Test_PresidentDisplayRank_ShouldMatchTheCardLabel(int rank, string french, string english)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PresidentCard.DisplayRank(rank, FRENCH), Is.EqualTo(french));
            Assert.That(PresidentCard.DisplayRank(rank, ENGLISH), Is.EqualTo(english));
        }
    }
}
