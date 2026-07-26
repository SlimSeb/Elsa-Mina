using ElsaMina.Commands.Games.Cards;
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
    [TestCase("jh", Suit.Hearts, TarotCard.JACK)]
    [TestCase("cs", Suit.Spades, TarotCard.CAVALIER)]
    [TestCase("qd", Suit.Diamonds, TarotCard.QUEEN)]
    [TestCase("kc", Suit.Clubs, TarotCard.KING)]
    [TestCase("10h", Suit.Hearts, 10)]
    public void Test_TarotCard_ShouldProduceTheExpectedToken(string expectedToken, Suit suit, int rank)
    {
        Assert.That(TarotCard.Suited(suit, rank).ToToken(), Is.EqualTo(expectedToken));
    }

    [TestCase("t1", TarotCard.PETIT)]
    [TestCase("t21", TarotCard.MONDE)]
    public void Test_TarotTrump_ShouldProduceTheExpectedToken(string expectedToken, int rank)
    {
        Assert.That(TarotCard.Trump(rank).ToToken(), Is.EqualTo(expectedToken));
    }

    [Test]
    public void Test_TarotExcuse_ShouldProduceTheExpectedToken()
    {
        Assert.That(TarotCard.Excuse.ToToken(), Is.EqualTo("exc"));
    }

    [TestCase("jh", Suit.Hearts, BeloteCard.JACK)]
    [TestCase("qs", Suit.Spades, BeloteCard.QUEEN)]
    [TestCase("kd", Suit.Diamonds, BeloteCard.KING)]
    [TestCase("ac", Suit.Clubs, BeloteCard.ACE)]
    [TestCase("7h", Suit.Hearts, 7)]
    [TestCase("10s", Suit.Spades, 10)]
    public void Test_BeloteCard_ShouldProduceTheExpectedToken(string expectedToken, Suit suit, int rank)
    {
        Assert.That(new BeloteCard(suit, rank).ToToken(), Is.EqualTo(expectedToken));
    }

    [TestCase("jh", Suit.Hearts, PresidentCard.JACK)]
    [TestCase("qs", Suit.Spades, PresidentCard.QUEEN)]
    [TestCase("kd", Suit.Diamonds, PresidentCard.KING)]
    [TestCase("ac", Suit.Clubs, PresidentCard.ACE)]
    [TestCase("2h", Suit.Hearts, PresidentCard.TWO)]
    [TestCase("3s", Suit.Spades, 3)]
    [TestCase("10d", Suit.Diamonds, 10)]
    public void Test_PresidentCard_ShouldProduceTheExpectedToken(string expectedToken, Suit suit,
        int rank)
    {
        Assert.That(new PresidentCard(suit, rank).ToToken(), Is.EqualTo(expectedToken));
    }

    /// <summary>
    /// The alternative spellings players type in chat, which <see cref="TarotCard.Parse"/> accepts
    /// alongside the canonical tokens.
    /// </summary>
    [TestCase("excuse")]
    [TestCase("x")]
    [TestCase("fool")]
    public void Test_TarotExcuse_ShouldAcceptTheAlternativeSpellings(string token)
    {
        Assert.That(TarotCard.Parse(token), Is.EqualTo(TarotCard.Excuse));
    }

    [TestCase("petit", TarotCard.PETIT)]
    [TestCase("monde", TarotCard.MONDE)]
    [TestCase("world", TarotCard.MONDE)]
    public void Test_TarotTrump_ShouldAcceptTheAlternativeSpellings(string token, int rank)
    {
        Assert.That(TarotCard.Parse(token), Is.EqualTo(TarotCard.Trump(rank)));
    }

    [TestCase("  KH  ", Suit.Hearts, TarotCard.KING)]
    [TestCase("qd", Suit.Diamonds, TarotCard.QUEEN)]
    public void Test_TarotCard_ShouldAcceptTheAlternativeSpellings(string token, Suit suit, int rank)
    {
        Assert.That(TarotCard.Parse(token), Is.EqualTo(TarotCard.Suited(suit, rank)));
    }

    [TestCase("vh", Suit.Hearts, BeloteCard.JACK)]
    [TestCase("rs", Suit.Spades, BeloteCard.KING)]
    [TestCase("1d", Suit.Diamonds, BeloteCard.ACE)]
    [TestCase(" AC ", Suit.Clubs, BeloteCard.ACE)]
    public void Test_BeloteCard_ShouldAcceptTheAlternativeSpellings(string token, Suit suit, int rank)
    {
        Assert.That(BeloteCard.Parse(token), Is.EqualTo(new BeloteCard(suit, rank)));
    }

    [TestCase("th", Suit.Hearts, 10)]
    [TestCase("1s", Suit.Spades, PresidentCard.ACE)]
    public void Test_PresidentCard_ShouldAcceptTheAlternativeSpellings(string token, Suit suit,
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

    [TestCase("h", Suit.Hearts)]
    [TestCase("coeur", Suit.Hearts)]
    [TestCase("cœurs", Suit.Hearts)]
    [TestCase("pique", Suit.Spades)]
    [TestCase("carreaux", Suit.Diamonds)]
    [TestCase("trèfle", Suit.Clubs)]
    [TestCase("clubs", Suit.Clubs)]
    public void Test_BeloteSuit_ShouldParseTheAcceptedSpellings(string token, Suit expected)
    {
        Assert.That(CardToken.ParseSuitName(token), Is.EqualTo(expected));
    }

    [TestCase(Suit.Hearts, "h")]
    [TestCase(Suit.Spades, "s")]
    [TestCase(Suit.Diamonds, "d")]
    [TestCase(Suit.Clubs, "c")]
    public void Test_BeloteSuitToken_ShouldRoundTripThroughParseSuit(Suit suit, string expectedToken)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(CardToken.SuitLetter(suit), Is.EqualTo(expectedToken));
            Assert.That(CardToken.ParseSuitName(CardToken.SuitLetter(suit)), Is.EqualTo(suit));
        }
    }
}
