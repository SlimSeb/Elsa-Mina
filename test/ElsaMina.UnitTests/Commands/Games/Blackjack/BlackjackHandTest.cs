using ElsaMina.Commands.Games.Blackjack;

namespace ElsaMina.UnitTests.Commands.Games.Blackjack;

public class BlackjackHandTest
{
    private static BlackjackHand CreateHand(params int[] ranks)
    {
        var hand = new BlackjackHand();
        foreach (var rank in ranks)
        {
            hand.Add(new BlackjackCard(rank, "♠"));
        }

        return hand;
    }

    [TestCase(new[] { 2, 3 }, 5)]
    [TestCase(new[] { 11, 12, 13 }, 30)]
    [TestCase(new[] { 1, 9 }, 20)]
    [TestCase(new[] { 1, 9, 5 }, 15)]
    [TestCase(new[] { 1, 1 }, 12)]
    [TestCase(new[] { 1, 1, 1, 13 }, 13)]
    public void Test_Value_ShouldCountAcesAsOneOrEleven(int[] ranks, int expectedValue)
    {
        // Act
        var value = CreateHand(ranks).Value;

        // Assert
        Assert.That(value, Is.EqualTo(expectedValue));
    }

    [Test]
    public void Test_IsBlackjack_ShouldBeTrue_WhenTwoCardsMakeTwentyOne()
    {
        Assert.That(CreateHand(1, 13).IsBlackjack, Is.True);
    }

    [Test]
    public void Test_IsBlackjack_ShouldBeFalse_WhenThreeCardsMakeTwentyOne()
    {
        Assert.That(CreateHand(7, 7, 7).IsBlackjack, Is.False);
    }

    [Test]
    public void Test_IsBust_ShouldBeTrue_WhenValueExceedsTwentyOne()
    {
        Assert.That(CreateHand(10, 10, 2).IsBust, Is.True);
    }

    [Test]
    public void Test_IsBust_ShouldBeFalse_WhenValueIsTwentyOne()
    {
        Assert.That(CreateHand(10, 10, 1).IsBust, Is.False);
    }
}
