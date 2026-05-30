using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class TarotTrickTest
{
    private static TarotPlayer Player(string id)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return new TarotPlayer(user);
    }

    [Test]
    public void Test_LeadSuit_ShouldBeFirstNonExcuseCard()
    {
        var trick = new TarotTrick();
        trick.Add(Player("a"), new TarotCard(TarotSuit.Excuse, 0));
        trick.Add(Player("b"), new TarotCard(TarotSuit.Spades, 5));

        Assert.That(trick.LeadSuit, Is.EqualTo(TarotSuit.Spades));
    }

    [Test]
    public void Test_DetermineWinner_ShouldBeHighestCardOfLeadSuit_WhenNoTrump()
    {
        var trick = new TarotTrick();
        var leader = Player("a");
        var winner = Player("b");
        trick.Add(leader, new TarotCard(TarotSuit.Hearts, 5));
        trick.Add(winner, new TarotCard(TarotSuit.Hearts, TarotCard.KING));
        trick.Add(Player("c"), new TarotCard(TarotSuit.Diamonds, TarotCard.KING)); // off-suit, ignored

        Assert.That(trick.DetermineWinner(), Is.EqualTo(winner));
    }

    [Test]
    public void Test_DetermineWinner_ShouldBeHighestTrump_WhenTrumpPlayed()
    {
        var trick = new TarotTrick();
        trick.Add(Player("a"), new TarotCard(TarotSuit.Hearts, TarotCard.KING));
        var winner = Player("b");
        trick.Add(winner, new TarotCard(TarotSuit.Trump, 12));
        trick.Add(Player("c"), new TarotCard(TarotSuit.Trump, 8));

        Assert.That(trick.DetermineWinner(), Is.EqualTo(winner));
    }

    [Test]
    public void Test_DetermineWinner_ShouldNeverBeTheExcuse()
    {
        var trick = new TarotTrick();
        var excusePlayer = Player("a");
        trick.Add(excusePlayer, new TarotCard(TarotSuit.Excuse, 0));
        var winner = Player("b");
        trick.Add(winner, new TarotCard(TarotSuit.Hearts, 3));

        Assert.That(trick.DetermineWinner(), Is.EqualTo(winner));
    }
}
