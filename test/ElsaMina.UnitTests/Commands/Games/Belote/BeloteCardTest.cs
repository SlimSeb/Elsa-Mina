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
        var card = new BeloteCard(BeloteSuit.Hearts, rank);

        Assert.That(card.GetPoints(BeloteSuit.Hearts), Is.EqualTo(expected));
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
        var card = new BeloteCard(BeloteSuit.Hearts, rank);

        Assert.That(card.GetPoints(BeloteSuit.Spades), Is.EqualTo(expected));
    }

    [Test]
    public void Test_GetStrength_ShouldRankJackHighest_WhenTrump()
    {
        var jack = new BeloteCard(BeloteSuit.Clubs, BeloteCard.JACK);
        var nine = new BeloteCard(BeloteSuit.Clubs, 9);
        var ace = new BeloteCard(BeloteSuit.Clubs, BeloteCard.ACE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(jack.GetStrength(BeloteSuit.Clubs), Is.GreaterThan(nine.GetStrength(BeloteSuit.Clubs)));
            Assert.That(nine.GetStrength(BeloteSuit.Clubs), Is.GreaterThan(ace.GetStrength(BeloteSuit.Clubs)));
        }
    }

    [Test]
    public void Test_GetStrength_ShouldRankAceHighest_WhenPlain()
    {
        var ace = new BeloteCard(BeloteSuit.Clubs, BeloteCard.ACE);
        var ten = new BeloteCard(BeloteSuit.Clubs, 10);
        var jack = new BeloteCard(BeloteSuit.Clubs, BeloteCard.JACK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ace.GetStrength(BeloteSuit.Hearts), Is.GreaterThan(ten.GetStrength(BeloteSuit.Hearts)));
            Assert.That(ten.GetStrength(BeloteSuit.Hearts), Is.GreaterThan(jack.GetStrength(BeloteSuit.Hearts)));
        }
    }

    [TestCase("ah", BeloteSuit.Hearts, BeloteCard.ACE)]
    [TestCase("7h", BeloteSuit.Hearts, 7)]
    [TestCase("10s", BeloteSuit.Spades, 10)]
    [TestCase("kd", BeloteSuit.Diamonds, BeloteCard.KING)]
    [TestCase("qc", BeloteSuit.Clubs, BeloteCard.QUEEN)]
    [TestCase("jc", BeloteSuit.Clubs, BeloteCard.JACK)]
    public void Test_Parse_ShouldReadValidTokens(string token, BeloteSuit suit, int rank)
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

    [TestCase("h", BeloteSuit.Hearts)]
    [TestCase("pique", BeloteSuit.Spades)]
    [TestCase("carreau", BeloteSuit.Diamonds)]
    [TestCase("clubs", BeloteSuit.Clubs)]
    public void Test_ParseSuit_ShouldReadNamesAndLetters(string token, BeloteSuit expected)
    {
        Assert.That(BeloteCard.ParseSuit(token), Is.EqualTo(expected));
    }
}