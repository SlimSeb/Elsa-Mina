using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class TarotRulesTest
{
    private static TarotCard Card(string token) => TarotCard.Parse(token);

    private static List<TarotCard> Hand(params string[] tokens) => tokens.Select(Card).ToList();

    private static TarotTrick TrickOf(params string[] tokens)
    {
        var trick = new TarotTrick();
        foreach (var token in tokens)
        {
            trick.Add(Seat("seat" + trick.Plays.Count), Card(token));
        }

        return trick;
    }

    private static TarotPlayer Seat(string id, bool isTaker = false, bool isPartner = false)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return new TarotPlayer(user) { IsTaker = isTaker, IsPartner = isPartner };
    }

    #region GetLegalMoves

    [Test]
    public void Test_GetLegalMoves_ShouldReturnTheWholeHand_WhenLeading()
    {
        var hand = Hand("3h", "10s", "t5");

        var legal = TarotRules.GetLegalMoves(hand, new TarotTrick(), 4, null, 1);

        Assert.That(legal, Is.EquivalentTo(hand));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldForceFollowingTheLeadSuit()
    {
        var legal = TarotRules.GetLegalMoves(Hand("3h", "kh", "10s", "t5"), TrickOf("5h"), 4, null, 1);

        Assert.That(legal, Is.EquivalentTo(Hand("3h", "kh")));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldForceTrumping_WhenTheLeadSuitIsMissing()
    {
        var legal = TarotRules.GetLegalMoves(Hand("10s", "t5", "t8"), TrickOf("5h"), 4, null, 1);

        Assert.That(legal, Is.EquivalentTo(Hand("t5", "t8")));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldForceOvertrumping_WhenAble()
    {
        var legal = TarotRules.GetLegalMoves(Hand("t5", "t12", "3s"), TrickOf("5h", "t10"), 4, null, 1);

        Assert.That(legal, Is.EquivalentTo(Hand("t12")));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldAllowAnyTrump_WhenUnableToOvertrump()
    {
        var legal = TarotRules.GetLegalMoves(Hand("t5", "t8", "3s"), TrickOf("5h", "t10"), 4, null, 1);

        Assert.That(legal, Is.EquivalentTo(Hand("t5", "t8")));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldAllowAnythingButTheExcuse_WhenHoldingNoTrumpAndNoLeadSuit()
    {
        var legal = TarotRules.GetLegalMoves(Hand("3s", "kd", "exc"), TrickOf("5h", "t10"), 4, null, 1);

        // The Excuse comes back in as the always-legal escape, but never as a forced discard.
        Assert.That(legal, Is.EquivalentTo(Hand("3s", "kd", "exc")));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldFollowTrump_WhenTrumpIsLed()
    {
        var legal = TarotRules.GetLegalMoves(Hand("t3", "t9", "kh"), TrickOf("t6"), 4, null, 1);

        Assert.That(legal, Is.EquivalentTo(Hand("t9")));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldAlwaysAllowTheExcuse()
    {
        var legal = TarotRules.GetLegalMoves(Hand("3h", "exc"), TrickOf("5h"), 4, null, 1);

        Assert.That(legal, Does.Contain(Card("exc")));
    }

    /// <summary>
    /// The Excuse does not fix the lead suit, so a trick holding nothing else is still open.
    /// </summary>
    [Test]
    public void Test_GetLegalMoves_ShouldTreatATrickHoldingOnlyTheExcuseAsALead()
    {
        var hand = Hand("3h", "10s", "t5");

        var legal = TarotRules.GetLegalMoves(hand, TrickOf("exc"), 4, null, 1);

        Assert.That(legal, Is.EquivalentTo(hand));
    }

    #endregion

    #region GetLegalLeadMoves

    [Test]
    public void Test_GetLegalLeadMoves_ShouldForbidTheCalledSuit_OnTheFirstTrickOfAFiveHandedGame()
    {
        var legal = TarotRules.GetLegalLeadMoves(Hand("2s", "ks", "kh", "t4"), 5, Card("ks"), 1);

        Assert.That(legal, Is.EquivalentTo(Hand("ks", "kh", "t4")));
    }

    [Test]
    public void Test_GetLegalLeadMoves_ShouldFallBackToTheWholeHand_WhenItHoldsNothingElse()
    {
        var hand = Hand("2s", "3s");

        var legal = TarotRules.GetLegalLeadMoves(hand, 5, Card("ks"), 1);

        Assert.That(legal, Is.EquivalentTo(hand));
    }

    [TestCase(4, 1, TestName = "four players")]
    [TestCase(5, 2, TestName = "five players, later trick")]
    public void Test_GetLegalLeadMoves_ShouldReturnTheWholeHand_WhenTheRestrictionDoesNotApply(int playerCount,
        int trickNumber)
    {
        var hand = Hand("2s", "ks", "kh");

        var legal = TarotRules.GetLegalLeadMoves(hand, playerCount, Card("ks"), trickNumber);

        Assert.That(legal, Is.EquivalentTo(hand));
    }

    [Test]
    public void Test_GetLegalLeadMoves_ShouldReturnTheWholeHand_WhenNoKingWasCalled()
    {
        var hand = Hand("2s", "ks", "kh");

        var legal = TarotRules.GetLegalLeadMoves(hand, 5, null, 1);

        Assert.That(legal, Is.EquivalentTo(hand));
    }

    #endregion

    #region Poignée

    [TestCase(4, 9, 0)]
    [TestCase(4, 10, 1)]
    [TestCase(4, 13, 2)]
    [TestCase(4, 15, 3)]
    [TestCase(3, 13, 1)]
    [TestCase(5, 8, 1)]
    public void Test_GetDeclarablePoigneeTier_ShouldFollowTheThresholdsOfThePlayerCount(int playerCount,
        int trumpCount, int expectedTier)
    {
        var hand = Enumerable.Range(1, trumpCount).Select(rank => Card($"t{rank}")).ToList();

        Assert.That(TarotRules.GetDeclarablePoigneeTier(hand, playerCount), Is.EqualTo(expectedTier));
    }

    /// <summary>
    /// The Excuse stands in for a missing trump, which is enough to reach the next tier.
    /// </summary>
    [Test]
    public void Test_GetDeclarablePoigneeTier_ShouldCountTheExcuseAsAMissingTrump()
    {
        var withoutExcuse = Enumerable.Range(1, 9).Select(rank => Card($"t{rank}")).ToList();
        var withExcuse = withoutExcuse.Append(Card("exc")).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TarotRules.GetDeclarablePoigneeTier(withoutExcuse, 4), Is.Zero);
            Assert.That(TarotRules.GetDeclarablePoigneeTier(withExcuse, 4), Is.EqualTo(1));
        }
    }

    [TestCase(0, 0)]
    [TestCase(9, 0)]
    [TestCase(10, 1)]
    [TestCase(12, 1)]
    [TestCase(13, 2)]
    [TestCase(14, 2)]
    [TestCase(15, 3)]
    [TestCase(18, 3)]
    public void Test_TierForTrumpCount_ShouldPickTheHighestReachedTier(int count, int expected)
    {
        Assert.That(TarotRules.TierForTrumpCount(count, [10, 13, 15]), Is.EqualTo(expected));
    }

    #endregion

    #region Misère

    [Test]
    public void Test_GetDeclarableMisereTypes_ShouldReportAMisereDAtout_WhenTheHandHoldsNoTrump()
    {
        var types = TarotRules.GetDeclarableMisereTypes(Hand("2h", "kh", "3s"));

        Assert.That(types, Is.EqualTo(new[] { TarotMisereType.Trump }));
    }

    [Test]
    public void Test_GetDeclarableMisereTypes_ShouldReportAMisereDeTete_WhenTheHandHoldsNoFaceCard()
    {
        var types = TarotRules.GetDeclarableMisereTypes(Hand("2h", "t5", "3s"));

        Assert.That(types, Is.EqualTo(new[] { TarotMisereType.Head }));
    }

    [Test]
    public void Test_GetDeclarableMisereTypes_ShouldReportBoth_WhenTheHandHoldsNeither()
    {
        var types = TarotRules.GetDeclarableMisereTypes(Hand("2h", "3s", "10d"));

        Assert.That(types, Is.EqualTo(new[] { TarotMisereType.Trump, TarotMisereType.Head }));
    }

    [Test]
    public void Test_GetDeclarableMisereTypes_ShouldReportNothing_WhenTheHandHoldsTrumpsAndFaces()
    {
        var types = TarotRules.GetDeclarableMisereTypes(Hand("t5", "kh"));

        Assert.That(types, Is.Empty);
    }

    /// <summary>
    /// The Excuse is neither a trump nor a face card, so it never spoils a misère.
    /// </summary>
    [Test]
    public void Test_GetDeclarableMisereTypes_ShouldTolerateTheExcuse()
    {
        var types = TarotRules.GetDeclarableMisereTypes(Hand("exc", "2h", "3s"));

        Assert.That(types, Is.EqualTo(new[] { TarotMisereType.Trump, TarotMisereType.Head }));
    }

    #endregion

    #region Petit au bout & kings

    [Test]
    public void Test_ComputePetitAuBoutSide_ShouldReturnZero_WhenThereIsNoLastTrick()
    {
        Assert.That(TarotRules.ComputePetitAuBoutSide(null, Seat("winner")), Is.Zero);
    }

    [Test]
    public void Test_ComputePetitAuBoutSide_ShouldReturnZero_WhenThePetitWasNotInTheLastTrick()
    {
        Assert.That(TarotRules.ComputePetitAuBoutSide(TrickOf("t2", "kh"), Seat("winner", isTaker: true)),
            Is.Zero);
    }

    [Test]
    public void Test_ComputePetitAuBoutSide_ShouldFavourTheTakerSide_WhenTheTakerWinsIt()
    {
        Assert.That(TarotRules.ComputePetitAuBoutSide(TrickOf("t1", "kh"), Seat("taker", isTaker: true)),
            Is.EqualTo(1));
    }

    [Test]
    public void Test_ComputePetitAuBoutSide_ShouldFavourTheTakerSide_WhenThePartnerWinsIt()
    {
        Assert.That(TarotRules.ComputePetitAuBoutSide(TrickOf("t1", "kh"), Seat("partner", isPartner: true)),
            Is.EqualTo(1));
    }

    [Test]
    public void Test_ComputePetitAuBoutSide_ShouldFavourTheDefenders_WhenADefenderWinsIt()
    {
        Assert.That(TarotRules.ComputePetitAuBoutSide(TrickOf("t1", "kh"), Seat("defender")), Is.EqualTo(-1));
    }

    [Test]
    public void Test_HoldsAllKings_ShouldBeTrue_OnlyWhenAllFourAreHeld()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TarotRules.HoldsAllKings(Hand("kh", "ks", "kd", "kc", "2h")), Is.True);
            Assert.That(TarotRules.HoldsAllKings(Hand("kh", "ks", "kd")), Is.False);
            Assert.That(TarotRules.HoldsAllKings(Hand()), Is.False);
        }
    }

    #endregion
}
