using ElsaMina.Commands.Games.Tarot;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class TarotScorerTest
{
    [TestCase(0, 112)]
    [TestCase(1, 102)]
    [TestCase(2, 82)]
    [TestCase(3, 72)]
    public void Test_GetTargetHalfPoints_ShouldReturnTargetForOudlerCount(int oudlers, int expected)
    {
        Assert.That(TarotScorer.GetTargetHalfPoints(oudlers), Is.EqualTo(expected));
    }

    [Test]
    public void Test_Compute_ShouldMarkContractMade_WhenTakerReachesTarget()
    {
        // 2 oudlers -> target 82 half-points; taker has 120.
        var result = TarotScorer.Compute(120, 2, TarotBid.Garde, 4, 0, -1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.True);
            Assert.That(result.DiffHalfPoints, Is.EqualTo(38));
            Assert.That(result.BaseHalfPoints, Is.EqualTo(88)); // 50 + 38
            Assert.That(result.Multiplier, Is.EqualTo(2));
            Assert.That(result.ContractValueHalfPoints, Is.EqualTo(176));
        }
    }

    [Test]
    public void Test_Compute_ShouldMarkContractFailed_WhenTakerFallsShort()
    {
        var result = TarotScorer.Compute(70, 2, TarotBid.Petite, 4, 0, -1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.False);
            Assert.That(result.DiffHalfPoints, Is.EqualTo(-12));
            Assert.That(result.BaseHalfPoints, Is.EqualTo(62));
        }
    }

    [Test]
    public void Test_Distribute_ShouldBeZeroSumForFourPlayers_WhenContractMade()
    {
        var deltas = TarotScorer.Distribute(100, playerCount: 4, takerIndex: 0, partnerIndex: -1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deltas, Is.EqualTo(new[] { 300, -100, -100, -100 }));
            Assert.That(deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Distribute_ShouldFlipSigns_WhenContractFailed()
    {
        var deltas = TarotScorer.Distribute(-100, playerCount: 4, takerIndex: 0, partnerIndex: -1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deltas, Is.EqualTo(new[] { -300, 100, 100, 100 }));
            Assert.That(deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Distribute_ShouldGiveTakerTwoSharesAndPartnerOne_ForFivePlayers()
    {
        var deltas = TarotScorer.Distribute(100, playerCount: 5, takerIndex: 0, partnerIndex: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deltas, Is.EqualTo(new[] { 200, 100, -100, -100, -100 }));
            Assert.That(deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Distribute_ShouldGiveTakerFourShares_ForFivePlayersWithoutPartner()
    {
        var deltas = TarotScorer.Distribute(100, playerCount: 5, takerIndex: 2, partnerIndex: -1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deltas, Is.EqualTo(new[] { -100, -100, 400, -100, -100 }));
            Assert.That(deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Distribute_ShouldBeZeroSumForThreePlayers()
    {
        var deltas = TarotScorer.Distribute(100, playerCount: 3, takerIndex: 1, partnerIndex: -1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deltas, Is.EqualTo(new[] { -100, 200, -100 }));
            Assert.That(deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Compute_ShouldAddPetitAuBout_FollowingTheMultiplier()
    {
        // 2 oudlers -> target 82; taker has 120 (diff 38). Garde (×2). Petit won by the taker side.
        var result = TarotScorer.Compute(120, 2, TarotBid.Garde, 4, 0, -1, petitAuBoutSide: 1);

        using (Assert.EnterMultipleScope())
        {
            // (50 + 38) × 2 = 176 contract, + 20 × 2 = 40 petit au bout = 216 per defender.
            Assert.That(result.PetitAuBoutHalfPoints, Is.EqualTo(40));
            Assert.That(result.PerDefenderHalfPoints, Is.EqualTo(216));
            Assert.That(result.Deltas, Is.EqualTo(new[] { 648, -216, -216, -216 }));
            Assert.That(result.Deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Compute_ShouldGivePetitAuBoutToDefenders_EvenWhenContractFailed()
    {
        // Failed contract, but the defenders won the Petit at the end: the bonus goes their way.
        var result = TarotScorer.Compute(70, 2, TarotBid.Petite, 4, 0, -1, petitAuBoutSide: -1);

        using (Assert.EnterMultipleScope())
        {
            // -(50 + 12) × 1 = -62, minus the 20 petit au bout = -82 per defender.
            Assert.That(result.Made, Is.False);
            Assert.That(result.PerDefenderHalfPoints, Is.EqualTo(-82));
            Assert.That(result.Deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Compute_ShouldAddPoigneeToTheDealWinner_RegardlessOfDeclarer()
    {
        var made = TarotScorer.Compute(120, 2, TarotBid.Petite, 4, 0, -1, poigneeHalfPoints: 40);
        var failed = TarotScorer.Compute(60, 2, TarotBid.Petite, 4, 0, -1, poigneeHalfPoints: 40);

        using (Assert.EnterMultipleScope())
        {
            // Made: (50 + 38) + 40 = 128 to the taker side.
            Assert.That(made.PerDefenderHalfPoints, Is.EqualTo(128));
            // Failed: -(50 + 22) - 40 = -112; the poignée now benefits the defenders.
            Assert.That(failed.PerDefenderHalfPoints, Is.EqualTo(-112));
        }
    }

    [Test]
    public void Test_Compute_ShouldAddSlamBonus()
    {
        var announced = TarotScorer.Compute(120, 2, TarotBid.Petite, 4, 0, -1,
            slamWinnerSide: 1, slamAnnounced: true);
        var unannounced = TarotScorer.Compute(120, 2, TarotBid.Petite, 4, 0, -1, slamWinnerSide: 1);
        var failed = TarotScorer.Compute(60, 2, TarotBid.Petite, 4, 0, -1, slamAnnounced: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(announced.SlamHalfPoints, Is.EqualTo(800));
            Assert.That(unannounced.SlamHalfPoints, Is.EqualTo(400));
            Assert.That(failed.SlamHalfPoints, Is.EqualTo(-400));
        }
    }
}
