using System.Globalization;
using ElsaMina.Commands.Games.Tarot;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class TarotCardTest
{
    [TestCase("kh", TarotSuit.Hearts, TarotCard.KING)]
    [TestCase("10s", TarotSuit.Spades, 10)]
    [TestCase("qd", TarotSuit.Diamonds, TarotCard.QUEEN)]
    [TestCase("cc", TarotSuit.Clubs, TarotCard.CAVALIER)]
    [TestCase("jh", TarotSuit.Hearts, TarotCard.JACK)]
    [TestCase("1d", TarotSuit.Diamonds, 1)]
    public void Test_Parse_ShouldReturnSuitCard_WhenTokenIsValid(string token, TarotSuit suit, int rank)
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

    [TestCase(TarotSuit.Trump, 21, "T21")]
    [TestCase(TarotSuit.Hearts, TarotCard.JACK, "J♥")]
    [TestCase(TarotSuit.Hearts, TarotCard.CAVALIER, "C♥")]
    [TestCase(TarotSuit.Hearts, TarotCard.QUEEN, "Q♥")]
    [TestCase(TarotSuit.Hearts, TarotCard.KING, "K♥")]
    public void Test_ToDisplay_ShouldUseDefaultNotation_WhenCultureIsNotFrench(TarotSuit suit, int rank, string expected)
    {
        var card = new TarotCard(suit, rank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.ToDisplay(), Is.EqualTo(expected));
            Assert.That(card.ToDisplay(new CultureInfo("en-US")), Is.EqualTo(expected));
        }
    }

    [TestCase(TarotSuit.Trump, 21, "A21")]
    [TestCase(TarotSuit.Hearts, TarotCard.JACK, "V♥")]
    [TestCase(TarotSuit.Hearts, TarotCard.CAVALIER, "C♥")]
    [TestCase(TarotSuit.Hearts, TarotCard.QUEEN, "D♥")]
    [TestCase(TarotSuit.Hearts, TarotCard.KING, "R♥")]
    public void Test_ToDisplay_ShouldUseFrenchNotation_WhenCultureIsFrench(TarotSuit suit, int rank, string expected)
    {
        var card = new TarotCard(suit, rank);

        Assert.That(card.ToDisplay(new CultureInfo("fr-FR")), Is.EqualTo(expected));
    }

    [Test]
    public void Test_ToDisplay_ShouldReturnExcuseEmoji_RegardlessOfCulture()
    {
        var excuse = new TarotCard(TarotSuit.Excuse, 0);

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
            Assert.That(new TarotCard(TarotSuit.Hearts, TarotCard.KING).HalfPoints, Is.EqualTo(9));
            Assert.That(new TarotCard(TarotSuit.Hearts, TarotCard.QUEEN).HalfPoints, Is.EqualTo(7));
            Assert.That(new TarotCard(TarotSuit.Hearts, TarotCard.CAVALIER).HalfPoints, Is.EqualTo(5));
            Assert.That(new TarotCard(TarotSuit.Hearts, TarotCard.JACK).HalfPoints, Is.EqualTo(3));
            Assert.That(new TarotCard(TarotSuit.Hearts, 7).HalfPoints, Is.EqualTo(1));
            Assert.That(new TarotCard(TarotSuit.Trump, 1).HalfPoints, Is.EqualTo(9));
            Assert.That(new TarotCard(TarotSuit.Trump, 21).HalfPoints, Is.EqualTo(9));
            Assert.That(new TarotCard(TarotSuit.Trump, 10).HalfPoints, Is.EqualTo(1));
            Assert.That(new TarotCard(TarotSuit.Excuse, 0).HalfPoints, Is.EqualTo(9));
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
