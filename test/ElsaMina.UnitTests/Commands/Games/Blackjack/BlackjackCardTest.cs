using ElsaMina.Commands.Games.Blackjack;

namespace ElsaMina.UnitTests.Commands.Games.Blackjack;

public class BlackjackCardTest
{
    [TestCase(1, "A")]
    [TestCase(7, "7")]
    [TestCase(10, "10")]
    [TestCase(11, "J")]
    [TestCase(12, "Q")]
    [TestCase(13, "K")]
    public void Test_DisplayRank_ShouldUseFaceLetters(int rank, string expectedDisplay)
    {
        Assert.That(new BlackjackCard(rank, "♠").DisplayRank, Is.EqualTo(expectedDisplay));
    }

    [TestCase(1, 11)]
    [TestCase(5, 5)]
    [TestCase(10, 10)]
    [TestCase(13, 10)]
    public void Test_BlackjackValue_ShouldCountFacesAsTen(int rank, int expectedValue)
    {
        Assert.That(new BlackjackCard(rank, "♠").BlackjackValue, Is.EqualTo(expectedValue));
    }

    [TestCase("♥", true)]
    [TestCase("♦", true)]
    [TestCase("♠", false)]
    [TestCase("♣", false)]
    public void Test_IsRed_ShouldBeTrue_WhenSuitIsHeartsOrDiamonds(string suit, bool expectedIsRed)
    {
        Assert.That(new BlackjackCard(1, suit).IsRed, Is.EqualTo(expectedIsRed));
    }
}
