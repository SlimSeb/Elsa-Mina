using ElsaMina.Commands.Games.Cards;
using ElsaMina.Commands.Games.Belote;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Belote;

[TestFixture]
public class BeloteTrickTest
{
    private static BelotePlayer Player(string id)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return new BelotePlayer(user);
    }

    [Test]
    public void Test_LeadSuit_ShouldBeFirstCardSuit()
    {
        var trick = new BeloteTrick(Suit.Clubs);
        trick.Add(Player("a"), new BeloteCard(Suit.Spades, 8));
        trick.Add(Player("b"), new BeloteCard(Suit.Hearts, BeloteCard.KING));

        Assert.That(trick.LeadSuit, Is.EqualTo(Suit.Spades));
    }

    [Test]
    public void Test_DetermineWinner_ShouldBeHighestCardOfLeadSuit_WhenNoTrump()
    {
        var trick = new BeloteTrick(Suit.Clubs);
        var leader = Player("a");
        var winner = Player("b");
        trick.Add(leader, new BeloteCard(Suit.Hearts, BeloteCard.KING));
        trick.Add(winner, new BeloteCard(Suit.Hearts, BeloteCard.ACE)); // ace beats king in plain suit
        trick.Add(Player("c"), new BeloteCard(Suit.Diamonds, BeloteCard.ACE)); // off-suit, ignored

        Assert.That(trick.DetermineWinner(), Is.EqualTo(winner));
    }

    [Test]
    public void Test_DetermineWinner_ShouldBeHighestTrump_WhenTrumpPlayed()
    {
        var trick = new BeloteTrick(Suit.Clubs);
        trick.Add(Player("a"), new BeloteCard(Suit.Hearts, BeloteCard.ACE));
        var winner = Player("b");
        trick.Add(winner, new BeloteCard(Suit.Clubs, 9)); // 9 of trump beats the 7 of trump
        trick.Add(Player("c"), new BeloteCard(Suit.Clubs, 7));

        Assert.That(trick.DetermineWinner(), Is.EqualTo(winner));
    }

    [Test]
    public void Test_DetermineWinner_ShouldRankTrumpJackAboveTrumpNine()
    {
        var trick = new BeloteTrick(Suit.Clubs);
        trick.Add(Player("a"), new BeloteCard(Suit.Clubs, 9));
        var winner = Player("b");
        trick.Add(winner, new BeloteCard(Suit.Clubs, BeloteCard.JACK));

        Assert.That(trick.DetermineWinner(), Is.EqualTo(winner));
    }
}