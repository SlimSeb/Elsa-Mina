using ElsaMina.Commands.Games.Cards;
using ElsaMina.Commands.Games.Belote;
using ElsaMina.Commands.Games.Tarot;
using ElsaMina.IntegrationTests.Fixtures;

namespace ElsaMina.IntegrationTests.Commands.Games.Rules;

/// <summary>
/// Pins who wins a trick. Tarot and belote run the same algorithm under different names, so both are
/// held to hand-written cases covering trumping, over-trumping, the Excuse, and belote's reordered
/// trump ranks (J &gt; 9 &gt; A) versus the plain order (A &gt; 10 &gt; K).
/// </summary>
[TestFixture]
public class TrickWinnerTest
{
    #region Tarot

    [Test]
    public void Test_TarotTrick_ShouldBeWonByTheHighestCardOfTheLeadSuit_WhenNoTrumpIsPlayed()
    {
        var trick = new TarotTrick();
        var lead = TarotSeat("lead");
        var high = TarotSeat("high");
        var offSuit = TarotSeat("offsuit");

        trick.Add(lead, TarotCard.Parse("5h"));
        trick.Add(high, TarotCard.Parse("kh"));
        trick.Add(offSuit, TarotCard.Parse("ks"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trick.LeadSuit, Is.EqualTo(Suit.Hearts));
            Assert.That(trick.HighestTrumpRank, Is.Null);
            Assert.That(trick.DetermineWinner(), Is.SameAs(high));
        }
    }

    [Test]
    public void Test_TarotTrick_ShouldBeWonByATrump_EvenWhenTheLeadSuitIsHigher()
    {
        var trick = new TarotTrick();
        var lead = TarotSeat("lead");
        var trumper = TarotSeat("trumper");

        trick.Add(lead, TarotCard.Parse("kh"));
        trick.Add(trumper, TarotCard.Parse("t1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trick.DetermineWinner(), Is.SameAs(trumper));
            Assert.That(trick.HighestTrumpRank, Is.EqualTo(1));
        }
    }

    [Test]
    public void Test_TarotTrick_ShouldBeWonByTheHighestTrump_WhenSeveralArePlayed()
    {
        var trick = new TarotTrick();
        var lead = TarotSeat("lead");
        var lowTrump = TarotSeat("lowtrump");
        var highTrump = TarotSeat("hightrump");

        trick.Add(lead, TarotCard.Parse("kh"));
        trick.Add(lowTrump, TarotCard.Parse("t8"));
        trick.Add(highTrump, TarotCard.Parse("t15"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trick.DetermineWinner(), Is.SameAs(highTrump));
            Assert.That(trick.HighestTrumpRank, Is.EqualTo(15));
        }
    }

    [Test]
    public void Test_TarotTrick_ShouldNeverBeWonByTheExcuse()
    {
        var trick = new TarotTrick();
        var excusePlayer = TarotSeat("excuse");
        var suitPlayer = TarotSeat("suit");

        trick.Add(excusePlayer, TarotCard.Parse("exc"));
        trick.Add(suitPlayer, TarotCard.Parse("2h"));

        using (Assert.EnterMultipleScope())
        {
            // The Excuse does not set the lead suit either: the next card does.
            Assert.That(trick.LeadSuit, Is.EqualTo(Suit.Hearts));
            Assert.That(trick.DetermineWinner(), Is.SameAs(suitPlayer));
        }
    }

    [Test]
    public void Test_TarotTrick_ShouldNotBeWonByTheExcuse_EvenAgainstTrumps()
    {
        var trick = new TarotTrick();
        var excusePlayer = TarotSeat("excuse");
        var trumper = TarotSeat("trumper");

        trick.Add(excusePlayer, TarotCard.Parse("exc"));
        trick.Add(trumper, TarotCard.Parse("t2"));

        Assert.That(trick.DetermineWinner(), Is.SameAs(trumper));
    }

    /// <summary>
    /// A trick containing nothing but the Excuse has no lead suit at all, and the opening seat is
    /// reported as its winner.
    /// </summary>
    [Test]
    public void Test_TarotTrick_ShouldFallBackToTheOpeningSeat_WhenOnlyTheExcuseWasPlayed()
    {
        var trick = new TarotTrick();
        var excusePlayer = TarotSeat("excuse");
        trick.Add(excusePlayer, TarotCard.Parse("exc"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trick.LeadSuit, Is.Null);
            Assert.That(trick.DetermineWinner(), Is.SameAs(excusePlayer));
        }
    }

    [Test]
    public void Test_TarotTrick_ShouldHaveNoWinner_WhenEmpty()
    {
        var trick = new TarotTrick();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trick.IsEmpty, Is.True);
            Assert.That(trick.DetermineWinner(), Is.Null);
        }
    }

    #endregion

    #region Belote

    [Test]
    public void Test_BeloteTrick_ShouldBeWonByTheHighestCardOfTheLeadSuit_WhenNoTrumpIsPlayed()
    {
        var trick = new BeloteTrick(Suit.Clubs);
        var lead = BeloteSeat("lead", 0);
        var ace = BeloteSeat("ace", 1);
        var offSuit = BeloteSeat("offsuit", 0);

        trick.Add(lead, BeloteCard.Parse("kh"));
        trick.Add(ace, BeloteCard.Parse("ah"));
        trick.Add(offSuit, BeloteCard.Parse("as"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trick.LeadSuit, Is.EqualTo(Suit.Hearts));
            Assert.That(trick.HighestTrumpStrength, Is.Null);
            Assert.That(trick.DetermineWinner(), Is.SameAs(ace));
        }
    }

    /// <summary>
    /// Outside trump the plain order applies, so a ten beats a king but loses to an ace.
    /// </summary>
    [Test]
    public void Test_BeloteTrick_ShouldRankPlainSuitsAceTenKing()
    {
        var trick = new BeloteTrick(Suit.Clubs);
        var king = BeloteSeat("king", 0);
        var ten = BeloteSeat("ten", 1);
        var jack = BeloteSeat("jack", 0);

        trick.Add(king, BeloteCard.Parse("kh"));
        trick.Add(ten, BeloteCard.Parse("10h"));
        trick.Add(jack, BeloteCard.Parse("jh"));

        Assert.That(trick.DetermineWinner(), Is.SameAs(ten));
    }

    /// <summary>
    /// Under trump the order changes: the jack is master, the nine second, and the ace only third.
    /// </summary>
    [Test]
    public void Test_BeloteTrick_ShouldRankTrumpJackNineAce()
    {
        var trick = new BeloteTrick(Suit.Hearts);
        var ace = BeloteSeat("ace", 0);
        var nine = BeloteSeat("nine", 1);
        var jack = BeloteSeat("jack", 0);

        trick.Add(ace, BeloteCard.Parse("ah"));
        trick.Add(nine, BeloteCard.Parse("9h"));
        trick.Add(jack, BeloteCard.Parse("jh"));

        Assert.That(trick.DetermineWinner(), Is.SameAs(jack));
    }

    [Test]
    public void Test_BeloteTrick_ShouldRankTheNineAboveTheAceUnderTrump()
    {
        var trick = new BeloteTrick(Suit.Hearts);
        var ace = BeloteSeat("ace", 0);
        var nine = BeloteSeat("nine", 1);

        trick.Add(ace, BeloteCard.Parse("ah"));
        trick.Add(nine, BeloteCard.Parse("9h"));

        Assert.That(trick.DetermineWinner(), Is.SameAs(nine));
    }

    [Test]
    public void Test_BeloteTrick_ShouldBeWonByATrump_EvenWhenTheLeadSuitIsHigher()
    {
        var trick = new BeloteTrick(Suit.Spades);
        var ace = BeloteSeat("ace", 0);
        var trumper = BeloteSeat("trumper", 1);

        trick.Add(ace, BeloteCard.Parse("ah"));
        trick.Add(trumper, BeloteCard.Parse("7s"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trick.DetermineWinner(), Is.SameAs(trumper));
            Assert.That(trick.HighestTrumpStrength, Is.EqualTo(1));
        }
    }

    [Test]
    public void Test_BeloteTrick_ShouldBeWonByTheStrongestTrump_WhenSeveralArePlayed()
    {
        var trick = new BeloteTrick(Suit.Spades);
        var ace = BeloteSeat("ace", 0);
        var lowTrump = BeloteSeat("lowtrump", 1);
        var highTrump = BeloteSeat("hightrump", 0);

        trick.Add(ace, BeloteCard.Parse("ah"));
        trick.Add(lowTrump, BeloteCard.Parse("7s"));
        trick.Add(highTrump, BeloteCard.Parse("js"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trick.DetermineWinner(), Is.SameAs(highTrump));
            Assert.That(trick.HighestTrumpStrength, Is.EqualTo(8));
        }
    }

    /// <summary>
    /// <c>CurrentWinner</c> is what the legal-move rules consult mid-trick to see whether a partner is
    /// already master, so it has to agree with the final winner at every point.
    /// </summary>
    [Test]
    public void Test_BeloteTrick_CurrentWinner_ShouldTrackTheWinnerAsCardsAreAdded()
    {
        var trick = new BeloteTrick(Suit.Spades);
        var lead = BeloteSeat("lead", 0);
        var follower = BeloteSeat("follower", 1);
        var trumper = BeloteSeat("trumper", 0);

        trick.Add(lead, BeloteCard.Parse("kh"));
        var afterLead = trick.CurrentWinner;
        trick.Add(follower, BeloteCard.Parse("ah"));
        var afterFollow = trick.CurrentWinner;
        trick.Add(trumper, BeloteCard.Parse("7s"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterLead, Is.SameAs(lead));
            Assert.That(afterFollow, Is.SameAs(follower));
            Assert.That(trick.CurrentWinner, Is.SameAs(trumper));
            Assert.That(trick.DetermineWinner(), Is.SameAs(trick.CurrentWinner));
        }
    }

    [Test]
    public void Test_BeloteTrick_ShouldHaveNoWinner_WhenEmpty()
    {
        var trick = new BeloteTrick(Suit.Hearts);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trick.IsEmpty, Is.True);
            Assert.That(trick.LeadSuit, Is.Null);
            Assert.That(trick.CurrentWinner, Is.Null);
        }
    }

    #endregion

    private static TarotPlayer TarotSeat(string id) => new(GameUsers.User(id));

    private static BelotePlayer BeloteSeat(string id, int team) => new(GameUsers.User(id)) { Team = team };
}
