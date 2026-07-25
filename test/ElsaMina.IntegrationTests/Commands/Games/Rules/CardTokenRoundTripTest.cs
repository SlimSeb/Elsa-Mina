using ElsaMina.Commands.Games.Belote;
using ElsaMina.Commands.Games.President;
using ElsaMina.Commands.Games.Tarot;

namespace ElsaMina.IntegrationTests.Commands.Games.Rules;

/// <summary>
/// Card tokens are not an internal detail: they are embedded in the <c>value</c> of the Showdown
/// buttons the game panels render, and in stored data. A token that stops round-tripping breaks live
/// games, so every card of every deck is checked both ways.
/// </summary>
[TestFixture]
public class CardTokenRoundTripTest
{
    private static IEnumerable<TarotCard> TarotDeck => TarotConstants.BuildDeck();
    private static IEnumerable<BeloteCard> BeloteDeck => BeloteConstants.BuildDeck();
    private static IEnumerable<PresidentCard> PresidentDeck => PresidentConstants.BuildDeck();

    [TestCaseSource(nameof(TarotDeck))]
    public void Test_TarotCard_ShouldRoundTripThroughItsToken(TarotCard card)
    {
        Assert.That(TarotCard.Parse(card.ToToken()), Is.EqualTo(card));
    }

    [TestCaseSource(nameof(BeloteDeck))]
    public void Test_BeloteCard_ShouldRoundTripThroughItsToken(BeloteCard card)
    {
        Assert.That(BeloteCard.Parse(card.ToToken()), Is.EqualTo(card));
    }

    [TestCaseSource(nameof(PresidentDeck))]
    public void Test_PresidentCard_ShouldRoundTripThroughItsToken(PresidentCard card)
    {
        Assert.That(PresidentCard.Parse(card.ToToken()), Is.EqualTo(card));
    }

    [TestCaseSource(nameof(PresidentDeck))]
    public void Test_PresidentRank_ShouldRoundTripThroughItsRankToken(PresidentCard card)
    {
        Assert.That(PresidentCard.ParseRank(PresidentCard.RankToken(card.Rank)), Is.EqualTo(card.Rank));
    }

    [Test]
    public void Test_TarotDeck_ShouldProduceSeventyEightDistinctTokens()
    {
        var tokens = TarotDeck.Select(card => card.ToToken()).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokens, Has.Count.EqualTo(78));
            Assert.That(tokens.Distinct().Count(), Is.EqualTo(78));
        }
    }

    [Test]
    public void Test_BeloteDeck_ShouldProduceThirtyTwoDistinctTokens()
    {
        var tokens = BeloteDeck.Select(card => card.ToToken()).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokens, Has.Count.EqualTo(32));
            Assert.That(tokens.Distinct().Count(), Is.EqualTo(32));
        }
    }

    [Test]
    public void Test_PresidentDeck_ShouldProduceFiftyTwoDistinctTokens()
    {
        var tokens = PresidentDeck.Select(card => card.ToToken()).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokens, Has.Count.EqualTo(52));
            Assert.That(tokens.Distinct().Count(), Is.EqualTo(52));
        }
    }

    /// <summary>
    /// The exact tokens the panel buttons carry, pinned so a refactor of the token helpers cannot
    /// quietly change the wire format.
    /// </summary>
    [TestCase("exc", TarotSuit.Excuse, 0)]
    [TestCase("t1", TarotSuit.Trump, TarotCard.PETIT)]
    [TestCase("t21", TarotSuit.Trump, TarotCard.MONDE)]
    [TestCase("jh", TarotSuit.Hearts, TarotCard.JACK)]
    [TestCase("cs", TarotSuit.Spades, TarotCard.CAVALIER)]
    [TestCase("qd", TarotSuit.Diamonds, TarotCard.QUEEN)]
    [TestCase("kc", TarotSuit.Clubs, TarotCard.KING)]
    [TestCase("10h", TarotSuit.Hearts, 10)]
    public void Test_TarotCard_ShouldProduceTheExpectedToken(string expectedToken, TarotSuit suit, int rank)
    {
        Assert.That(new TarotCard(suit, rank).ToToken(), Is.EqualTo(expectedToken));
    }

    [TestCase("jh", BeloteSuit.Hearts, BeloteCard.JACK)]
    [TestCase("qs", BeloteSuit.Spades, BeloteCard.QUEEN)]
    [TestCase("kd", BeloteSuit.Diamonds, BeloteCard.KING)]
    [TestCase("ac", BeloteSuit.Clubs, BeloteCard.ACE)]
    [TestCase("7h", BeloteSuit.Hearts, 7)]
    [TestCase("10s", BeloteSuit.Spades, 10)]
    public void Test_BeloteCard_ShouldProduceTheExpectedToken(string expectedToken, BeloteSuit suit, int rank)
    {
        Assert.That(new BeloteCard(suit, rank).ToToken(), Is.EqualTo(expectedToken));
    }

    [TestCase("jh", PresidentSuit.Hearts, PresidentCard.JACK)]
    [TestCase("qs", PresidentSuit.Spades, PresidentCard.QUEEN)]
    [TestCase("kd", PresidentSuit.Diamonds, PresidentCard.KING)]
    [TestCase("ac", PresidentSuit.Clubs, PresidentCard.ACE)]
    [TestCase("2h", PresidentSuit.Hearts, PresidentCard.TWO)]
    [TestCase("3s", PresidentSuit.Spades, 3)]
    [TestCase("10d", PresidentSuit.Diamonds, 10)]
    public void Test_PresidentCard_ShouldProduceTheExpectedToken(string expectedToken, PresidentSuit suit,
        int rank)
    {
        Assert.That(new PresidentCard(suit, rank).ToToken(), Is.EqualTo(expectedToken));
    }

    /// <summary>
    /// The alternative spellings players type in chat, which <see cref="TarotCard.Parse"/> accepts
    /// alongside the canonical tokens.
    /// </summary>
    [TestCase("excuse", TarotSuit.Excuse, 0)]
    [TestCase("x", TarotSuit.Excuse, 0)]
    [TestCase("fool", TarotSuit.Excuse, 0)]
    [TestCase("petit", TarotSuit.Trump, TarotCard.PETIT)]
    [TestCase("monde", TarotSuit.Trump, TarotCard.MONDE)]
    [TestCase("world", TarotSuit.Trump, TarotCard.MONDE)]
    [TestCase("  KH  ", TarotSuit.Hearts, TarotCard.KING)]
    public void Test_TarotCard_ShouldAcceptTheAlternativeSpellings(string token, TarotSuit suit, int rank)
    {
        Assert.That(TarotCard.Parse(token), Is.EqualTo(new TarotCard(suit, rank)));
    }

    [TestCase("vh", BeloteSuit.Hearts, BeloteCard.JACK)]
    [TestCase("rs", BeloteSuit.Spades, BeloteCard.KING)]
    [TestCase("1d", BeloteSuit.Diamonds, BeloteCard.ACE)]
    [TestCase(" AC ", BeloteSuit.Clubs, BeloteCard.ACE)]
    public void Test_BeloteCard_ShouldAcceptTheAlternativeSpellings(string token, BeloteSuit suit, int rank)
    {
        Assert.That(BeloteCard.Parse(token), Is.EqualTo(new BeloteCard(suit, rank)));
    }

    [TestCase("th", PresidentSuit.Hearts, 10)]
    [TestCase("1s", PresidentSuit.Spades, PresidentCard.ACE)]
    public void Test_PresidentCard_ShouldAcceptTheAlternativeSpellings(string token, PresidentSuit suit,
        int rank)
    {
        Assert.That(PresidentCard.Parse(token), Is.EqualTo(new PresidentCard(suit, rank)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("zz")]
    [TestCase("t22")]
    [TestCase("t0")]
    [TestCase("15h")]
    public void Test_TarotCard_ShouldRejectAnythingThatIsNotACard(string token)
    {
        Assert.That(TarotCard.Parse(token), Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("h")]
    [TestCase("6h")]
    [TestCase("11h")]
    [TestCase("az")]
    public void Test_BeloteCard_ShouldRejectAnythingThatIsNotACard(string token)
    {
        Assert.That(BeloteCard.Parse(token), Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("h")]
    [TestCase("2z")]
    [TestCase("11h")]
    public void Test_PresidentCard_ShouldRejectAnythingThatIsNotACard(string token)
    {
        Assert.That(PresidentCard.Parse(token), Is.Null);
    }

    [TestCase("h", BeloteSuit.Hearts)]
    [TestCase("coeur", BeloteSuit.Hearts)]
    [TestCase("cœurs", BeloteSuit.Hearts)]
    [TestCase("pique", BeloteSuit.Spades)]
    [TestCase("carreaux", BeloteSuit.Diamonds)]
    [TestCase("trèfle", BeloteSuit.Clubs)]
    [TestCase("clubs", BeloteSuit.Clubs)]
    public void Test_BeloteSuit_ShouldParseTheAcceptedSpellings(string token, BeloteSuit expected)
    {
        Assert.That(BeloteCard.ParseSuit(token), Is.EqualTo(expected));
    }

    [TestCase(BeloteSuit.Hearts, "h")]
    [TestCase(BeloteSuit.Spades, "s")]
    [TestCase(BeloteSuit.Diamonds, "d")]
    [TestCase(BeloteSuit.Clubs, "c")]
    public void Test_BeloteSuitToken_ShouldRoundTripThroughParseSuit(BeloteSuit suit, string expectedToken)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BeloteCard.SuitToken(suit), Is.EqualTo(expectedToken));
            Assert.That(BeloteCard.ParseSuit(BeloteCard.SuitToken(suit)), Is.EqualTo(suit));
        }
    }
}
