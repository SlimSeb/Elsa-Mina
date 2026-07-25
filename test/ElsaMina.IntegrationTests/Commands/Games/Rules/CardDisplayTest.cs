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

    [TestCase(TarotSuit.Hearts, TarotCard.JACK, "V♥", "J♥")]
    [TestCase(TarotSuit.Hearts, TarotCard.CAVALIER, "C♥", "C♥")]
    [TestCase(TarotSuit.Hearts, TarotCard.QUEEN, "D♥", "Q♥")]
    [TestCase(TarotSuit.Hearts, TarotCard.KING, "R♥", "K♥")]
    [TestCase(TarotSuit.Spades, TarotCard.KING, "R♠", "K♠")]
    [TestCase(TarotSuit.Diamonds, TarotCard.QUEEN, "D♦", "Q♦")]
    [TestCase(TarotSuit.Clubs, TarotCard.JACK, "V♣", "J♣")]
    [TestCase(TarotSuit.Hearts, 1, "1♥", "1♥")]
    [TestCase(TarotSuit.Spades, 10, "10♠", "10♠")]
    [TestCase(TarotSuit.Trump, TarotCard.PETIT, "A1", "T1")]
    [TestCase(TarotSuit.Trump, 12, "A12", "T12")]
    [TestCase(TarotSuit.Trump, TarotCard.MONDE, "A21", "T21")]
    [TestCase(TarotSuit.Excuse, 0, "🃏", "🃏")]
    public void Test_TarotCard_ShouldDisplayTheExpectedLabel(TarotSuit suit, int rank, string french,
        string english)
    {
        var card = new TarotCard(suit, rank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.ToDisplay(FRENCH), Is.EqualTo(french));
            Assert.That(card.ToDisplay(ENGLISH), Is.EqualTo(english));
        }
    }

    [TestCase(BeloteSuit.Hearts, BeloteCard.JACK, "V♥", "J♥")]
    [TestCase(BeloteSuit.Hearts, BeloteCard.QUEEN, "D♥", "Q♥")]
    [TestCase(BeloteSuit.Hearts, BeloteCard.KING, "R♥", "K♥")]
    [TestCase(BeloteSuit.Hearts, BeloteCard.ACE, "A♥", "A♥")]
    [TestCase(BeloteSuit.Spades, BeloteCard.KING, "R♠", "K♠")]
    [TestCase(BeloteSuit.Diamonds, 10, "10♦", "10♦")]
    [TestCase(BeloteSuit.Clubs, 7, "7♣", "7♣")]
    public void Test_BeloteCard_ShouldDisplayTheExpectedLabel(BeloteSuit suit, int rank, string french,
        string english)
    {
        var card = new BeloteCard(suit, rank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.ToDisplay(FRENCH), Is.EqualTo(french));
            Assert.That(card.ToDisplay(ENGLISH), Is.EqualTo(english));
        }
    }

    [TestCase(PresidentSuit.Hearts, PresidentCard.JACK, "V♥", "J♥")]
    [TestCase(PresidentSuit.Hearts, PresidentCard.QUEEN, "D♥", "Q♥")]
    [TestCase(PresidentSuit.Hearts, PresidentCard.KING, "R♥", "K♥")]
    [TestCase(PresidentSuit.Hearts, PresidentCard.ACE, "A♥", "A♥")]
    [TestCase(PresidentSuit.Hearts, PresidentCard.TWO, "2♥", "2♥")]
    [TestCase(PresidentSuit.Spades, 3, "3♠", "3♠")]
    [TestCase(PresidentSuit.Diamonds, 10, "10♦", "10♦")]
    [TestCase(PresidentSuit.Clubs, PresidentCard.KING, "R♣", "K♣")]
    public void Test_PresidentCard_ShouldDisplayTheExpectedLabel(PresidentSuit suit, int rank, string french,
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
    [TestCase(PokerSuit.Hearts, PokerCard.JACK, "J♥")]
    [TestCase(PokerSuit.Diamonds, PokerCard.QUEEN, "Q♦")]
    [TestCase(PokerSuit.Spades, PokerCard.KING, "K♠")]
    [TestCase(PokerSuit.Clubs, PokerCard.ACE, "A♣")]
    [TestCase(PokerSuit.Hearts, 10, "10♥")]
    [TestCase(PokerSuit.Spades, 2, "2♠")]
    public void Test_PokerCard_ShouldDisplayTheExpectedLabel(PokerSuit suit, int rank, string expected)
    {
        Assert.That(new PokerCard(suit, rank).ToDisplay(), Is.EqualTo(expected));
    }

    [TestCase(PokerSuit.Hearts, true)]
    [TestCase(PokerSuit.Diamonds, true)]
    [TestCase(PokerSuit.Spades, false)]
    [TestCase(PokerSuit.Clubs, false)]
    public void Test_PokerCard_ShouldKnowWhichSuitsAreRed(PokerSuit suit, bool expected)
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
            Assert.That(new TarotCard(TarotSuit.Hearts, TarotCard.KING).ToDisplay(), Is.EqualTo("K♥"));
            Assert.That(new TarotCard(TarotSuit.Trump, 5).ToDisplay(), Is.EqualTo("T5"));
            Assert.That(new BeloteCard(BeloteSuit.Spades, BeloteCard.JACK).ToDisplay(), Is.EqualTo("J♠"));
            Assert.That(new PresidentCard(PresidentSuit.Clubs, PresidentCard.QUEEN).ToDisplay(),
                Is.EqualTo("Q♣"));
        }
    }

    [TestCase(BeloteSuit.Hearts, "♥")]
    [TestCase(BeloteSuit.Spades, "♠")]
    [TestCase(BeloteSuit.Diamonds, "♦")]
    [TestCase(BeloteSuit.Clubs, "♣")]
    public void Test_BeloteSuitDisplay_ShouldUseTheExpectedSymbol(BeloteSuit suit, string expected)
    {
        Assert.That(BeloteCard.SuitDisplay(suit), Is.EqualTo(expected));
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
