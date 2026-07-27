using ElsaMina.Commands.Games.Cards;
using System.Globalization;
using ElsaMina.Commands.Games.President;

namespace ElsaMina.UnitTests.Commands.Games.President;

[TestFixture]
public class PresidentCardTest
{
    [TestCase("3h", Suit.Hearts, 3)]
    [TestCase("10s", Suit.Spades, 10)]
    [TestCase("ts", Suit.Spades, 10)]
    [TestCase("jd", Suit.Diamonds, PresidentCard.JACK)]
    [TestCase("qc", Suit.Clubs, PresidentCard.QUEEN)]
    [TestCase("kh", Suit.Hearts, PresidentCard.KING)]
    [TestCase("as", Suit.Spades, PresidentCard.ACE)]
    [TestCase("2d", Suit.Diamonds, PresidentCard.TWO)]
    [TestCase("  KH ", Suit.Hearts, PresidentCard.KING)]
    public void Test_Parse_ShouldReturnCard_WhenTokenIsValid(string token, Suit expectedSuit,
        int expectedRank)
    {
        var card = PresidentCard.Parse(token);

        Assert.That(card, Is.EqualTo(new PresidentCard(expectedSuit, expectedRank)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("h")]
    [TestCase("11h")]
    [TestCase("0s")]
    [TestCase("kx")]
    [TestCase("foo")]
    public void Test_Parse_ShouldReturnNull_WhenTokenIsInvalid(string token)
    {
        Assert.That(PresidentCard.Parse(token), Is.Null);
    }

    [Test]
    public void Test_ToToken_ShouldRoundTripThroughParse_ForTheWholeDeck()
    {
        foreach (var card in PresidentConstants.BuildDeck())
        {
            Assert.That(PresidentCard.Parse(card.ToToken()), Is.EqualTo(card));
        }
    }

    [Test]
    public void Test_BuildDeck_ShouldContain52DistinctCards()
    {
        var deck = PresidentConstants.BuildDeck();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deck, Has.Count.EqualTo(52));
            Assert.That(deck.Distinct().Count(), Is.EqualTo(52));
            Assert.That(deck.Count(card => card.IsTwo), Is.EqualTo(4));
        }
    }

    [Test]
    public void Test_ToDisplay_ShouldUseEnglishFaceLetters_WhenCultureIsInvariant()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(new PresidentCard(Suit.Hearts, PresidentCard.KING).ToDisplay(), Is.EqualTo("K♥"));
            Assert.That(new PresidentCard(Suit.Spades, 10).ToDisplay(), Is.EqualTo("10♠"));
            Assert.That(new PresidentCard(Suit.Diamonds, PresidentCard.TWO).ToDisplay(), Is.EqualTo("2♦"));
        }
    }

    [Test]
    public void Test_ToDisplay_ShouldUseFrenchFaceLetters_WhenCultureIsFrench()
    {
        var french = new CultureInfo("fr-FR");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(new PresidentCard(Suit.Hearts, PresidentCard.KING).ToDisplay(french),
                Is.EqualTo("R♥"));
            Assert.That(new PresidentCard(Suit.Clubs, PresidentCard.QUEEN).ToDisplay(french),
                Is.EqualTo("D♣"));
            Assert.That(new PresidentCard(Suit.Spades, PresidentCard.JACK).ToDisplay(french),
                Is.EqualTo("V♠"));
        }
    }

    [Test]
    public void Test_RankOrder_ShouldRankTwoAboveAce()
    {
        Assert.That(PresidentCard.TWO, Is.GreaterThan(PresidentCard.ACE));
    }
}
