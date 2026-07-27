using ElsaMina.Commands.Games.Cards;
using System.Globalization;
using ElsaMina.Commands.Games.Tarot;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class TarotCardTest
{
    [TestCase("kh", Suit.Hearts, TarotCard.KING)]
    [TestCase("10s", Suit.Spades, 10)]
    [TestCase("qd", Suit.Diamonds, TarotCard.QUEEN)]
    [TestCase("cc", Suit.Clubs, TarotCard.CAVALIER)]
    [TestCase("jh", Suit.Hearts, TarotCard.JACK)]
    [TestCase("1d", Suit.Diamonds, 1)]
    public void Test_Parse_ShouldReturnSuitCard_WhenTokenIsValid(string token, Suit suit, int rank)
    {
        var card = TarotCard.Parse(token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Suit, Is.EqualTo(suit));
            Assert.That(card.Rank, Is.EqualTo(rank));
        }
    }

    [TestCase("t1", 1)]
    [TestCase("t21", 21)]
    [TestCase("petit", 1)]
    [TestCase("monde", 21)]
    public void Test_Parse_ShouldReturnTrump_WhenTokenIsTrump(string token, int rank)
    {
        var card = TarotCard.Parse(token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.IsTrump, Is.True);
            Assert.That(card.Rank, Is.EqualTo(rank));
        }
    }

    [TestCase("exc")]
    [TestCase("excuse")]
    [TestCase("x")]
    public void Test_Parse_ShouldReturnExcuse_WhenTokenIsExcuse(string token)
    {
        Assert.That(TarotCard.Parse(token).IsExcuse, Is.True);
    }

    [TestCase("")]
    [TestCase("zz")]
    [TestCase("t99")]
    [TestCase("15h")]
    [TestCase("kz")]
    public void Test_Parse_ShouldReturnNull_WhenTokenIsInvalid(string token)
    {
        Assert.That(TarotCard.Parse(token), Is.Null);
    }

    [Test]
    public void Test_ToToken_ShouldRoundTripForEveryCardInTheDeck()
    {
        foreach (var card in TarotConstants.BuildDeck())
        {
            Assert.That(TarotCard.Parse(card.ToToken()), Is.EqualTo(card), $"failed for {card.ToToken()}");
        }
    }

    [TestCase(Suit.Hearts, TarotCard.JACK, "J♥")]
    [TestCase(Suit.Hearts, TarotCard.CAVALIER, "C♥")]
    [TestCase(Suit.Hearts, TarotCard.QUEEN, "Q♥")]
    [TestCase(Suit.Hearts, TarotCard.KING, "K♥")]
    public void Test_ToDisplay_ShouldUseDefaultNotation_WhenCultureIsNotFrench(Suit suit, int rank, string expected)
    {
        var card = TarotCard.Suited(suit, rank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.ToDisplay(), Is.EqualTo(expected));
            Assert.That(card.ToDisplay(new CultureInfo("en-US")), Is.EqualTo(expected));
        }
    }

    [TestCase(Suit.Hearts, TarotCard.JACK, "V♥")]
    [TestCase(Suit.Hearts, TarotCard.CAVALIER, "C♥")]
    [TestCase(Suit.Hearts, TarotCard.QUEEN, "D♥")]
    [TestCase(Suit.Hearts, TarotCard.KING, "R♥")]
    public void Test_ToDisplay_ShouldUseFrenchNotation_WhenCultureIsFrench(Suit suit, int rank, string expected)
    {
        var card = TarotCard.Suited(suit, rank);

        Assert.That(card.ToDisplay(new CultureInfo("fr-FR")), Is.EqualTo(expected));
    }

    [TestCase(21, "T21", "A21")]
    [TestCase(1, "T1", "A1")]
    public void Test_ToDisplay_ShouldPrefixTrumpsWithTheLocalLetter(int rank, string english, string french)
    {
        var card = TarotCard.Trump(rank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.ToDisplay(new CultureInfo("en-US")), Is.EqualTo(english));
            Assert.That(card.ToDisplay(new CultureInfo("fr-FR")), Is.EqualTo(french));
        }
    }

    [Test]
    public void Test_ToDisplay_ShouldReturnExcuseEmoji_RegardlessOfCulture()
    {
        var excuse = TarotCard.Excuse;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(excuse.ToDisplay(new CultureInfo("en-US")), Is.EqualTo("🃏"));
            Assert.That(excuse.ToDisplay(new CultureInfo("fr-FR")), Is.EqualTo("🃏"));
        }
    }

    [Test]
    public void Test_HalfPoints_ShouldMatchTarotValues()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TarotCard.Suited(Suit.Hearts, TarotCard.KING).HalfPoints, Is.EqualTo(9));
            Assert.That(TarotCard.Suited(Suit.Hearts, TarotCard.QUEEN).HalfPoints, Is.EqualTo(7));
            Assert.That(TarotCard.Suited(Suit.Hearts, TarotCard.CAVALIER).HalfPoints, Is.EqualTo(5));
            Assert.That(TarotCard.Suited(Suit.Hearts, TarotCard.JACK).HalfPoints, Is.EqualTo(3));
            Assert.That(TarotCard.Suited(Suit.Hearts, 7).HalfPoints, Is.EqualTo(1));
            Assert.That(TarotCard.Trump(1).HalfPoints, Is.EqualTo(9));
            Assert.That(TarotCard.Trump(21).HalfPoints, Is.EqualTo(9));
            Assert.That(TarotCard.Trump(10).HalfPoints, Is.EqualTo(1));
            Assert.That(TarotCard.Excuse.HalfPoints, Is.EqualTo(9));
        }
    }

    [Test]
    public void Test_BuildDeck_ShouldContainSeventyEightCardsWorthOneHundredEightyTwoHalfPoints()
    {
        var deck = TarotConstants.BuildDeck();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deck, Has.Count.EqualTo(78));
            Assert.That(deck.Distinct().Count(), Is.EqualTo(78));
            Assert.That(deck.Sum(card => card.HalfPoints), Is.EqualTo(182));
            Assert.That(deck.Count(card => card.IsOudler), Is.EqualTo(3));
        }
    }
}
