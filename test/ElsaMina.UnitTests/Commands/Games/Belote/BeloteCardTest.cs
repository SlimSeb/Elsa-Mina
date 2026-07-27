using ElsaMina.Commands.Games.Cards;
using ElsaMina.Commands.Games.Belote;

namespace ElsaMina.UnitTests.Commands.Games.Belote;

[TestFixture]
public class BeloteCardTest
{
    [TestCase(BeloteCard.JACK, 20)]
    [TestCase(9, 14)]
    [TestCase(BeloteCard.ACE, 11)]
    [TestCase(10, 10)]
    [TestCase(BeloteCard.KING, 4)]
    [TestCase(BeloteCard.QUEEN, 3)]
    [TestCase(8, 0)]
    [TestCase(7, 0)]
    public void Test_GetPoints_ShouldUseTrumpValues_WhenCardIsTrump(int rank, int expected)
    {
        var card = new BeloteCard(Suit.Hearts, rank);

        Assert.That(card.GetPoints(Suit.Hearts), Is.EqualTo(expected));
    }

    [TestCase(BeloteCard.ACE, 11)]
    [TestCase(10, 10)]
    [TestCase(BeloteCard.KING, 4)]
    [TestCase(BeloteCard.QUEEN, 3)]
    [TestCase(BeloteCard.JACK, 2)]
    [TestCase(9, 0)]
    [TestCase(8, 0)]
    [TestCase(7, 0)]
    public void Test_GetPoints_ShouldUsePlainValues_WhenCardIsNotTrump(int rank, int expected)
    {
        var card = new BeloteCard(Suit.Hearts, rank);

        Assert.That(card.GetPoints(Suit.Spades), Is.EqualTo(expected));
    }

    [Test]
    public void Test_GetStrength_ShouldRankJackHighest_WhenTrump()
    {
        var jack = new BeloteCard(Suit.Clubs, BeloteCard.JACK);
        var nine = new BeloteCard(Suit.Clubs, 9);
        var ace = new BeloteCard(Suit.Clubs, BeloteCard.ACE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(jack.GetStrength(Suit.Clubs), Is.GreaterThan(nine.GetStrength(Suit.Clubs)));
            Assert.That(nine.GetStrength(Suit.Clubs), Is.GreaterThan(ace.GetStrength(Suit.Clubs)));
        }
    }

    [Test]
    public void Test_GetStrength_ShouldRankAceHighest_WhenPlain()
    {
        var ace = new BeloteCard(Suit.Clubs, BeloteCard.ACE);
        var ten = new BeloteCard(Suit.Clubs, 10);
        var jack = new BeloteCard(Suit.Clubs, BeloteCard.JACK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ace.GetStrength(Suit.Hearts), Is.GreaterThan(ten.GetStrength(Suit.Hearts)));
            Assert.That(ten.GetStrength(Suit.Hearts), Is.GreaterThan(jack.GetStrength(Suit.Hearts)));
        }
    }

    [TestCase("ah", Suit.Hearts, BeloteCard.ACE)]
    [TestCase("7h", Suit.Hearts, 7)]
    [TestCase("10s", Suit.Spades, 10)]
    [TestCase("kd", Suit.Diamonds, BeloteCard.KING)]
    [TestCase("qc", Suit.Clubs, BeloteCard.QUEEN)]
    [TestCase("jc", Suit.Clubs, BeloteCard.JACK)]
    public void Test_Parse_ShouldReadValidTokens(string token, Suit suit, int rank)
    {
        Assert.That(BeloteCard.Parse(token), Is.EqualTo(new BeloteCard(suit, rank)));
    }

    [TestCase("")]
    [TestCase("zz")]
    [TestCase("6h")]
    [TestCase("11s")]
    public void Test_Parse_ShouldReturnNull_WhenInvalid(string token)
    {
        Assert.That(BeloteCard.Parse(token), Is.Null);
    }

    [Test]
    public void Test_ToToken_ShouldRoundTripThroughParse()
    {
        foreach (var card in BeloteConstants.BuildDeck())
        {
            Assert.That(BeloteCard.Parse(card.ToToken()), Is.EqualTo(card));
        }
    }

    [TestCase("h", Suit.Hearts)]
    [TestCase("pique", Suit.Spades)]
    [TestCase("carreau", Suit.Diamonds)]
    [TestCase("clubs", Suit.Clubs)]
    public void Test_ParseSuit_ShouldReadNamesAndLetters(string token, Suit expected)
    {
        Assert.That(CardToken.ParseSuitName(token), Is.EqualTo(expected));
    }
}