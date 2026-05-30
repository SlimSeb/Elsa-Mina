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
        var deltas = TarotScorer.Distribute(100, made: true, playerCount: 4, takerIndex: 0, partnerIndex: -1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deltas, Is.EqualTo(new[] { 300, -100, -100, -100 }));
            Assert.That(deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Distribute_ShouldFlipSigns_WhenContractFailed()
    {
        var deltas = TarotScorer.Distribute(100, made: false, playerCount: 4, takerIndex: 0, partnerIndex: -1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deltas, Is.EqualTo(new[] { -300, 100, 100, 100 }));
            Assert.That(deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Distribute_ShouldGiveTakerTwoSharesAndPartnerOne_ForFivePlayers()
    {
        var deltas = TarotScorer.Distribute(100, made: true, playerCount: 5, takerIndex: 0, partnerIndex: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deltas, Is.EqualTo(new[] { 200, 100, -100, -100, -100 }));
            Assert.That(deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Distribute_ShouldGiveTakerFourShares_ForFivePlayersWithoutPartner()
    {
        var deltas = TarotScorer.Distribute(100, made: true, playerCount: 5, takerIndex: 2, partnerIndex: -1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deltas, Is.EqualTo(new[] { -100, -100, 400, -100, -100 }));
            Assert.That(deltas.Sum(), Is.Zero);
        }
    }

    [Test]
    public void Test_Distribute_ShouldBeZeroSumForThreePlayers()
    {
        var deltas = TarotScorer.Distribute(100, made: true, playerCount: 3, takerIndex: 1, partnerIndex: -1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deltas, Is.EqualTo(new[] { -100, 200, -100 }));
            Assert.That(deltas.Sum(), Is.Zero);
        }
    }
}
