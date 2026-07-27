using ElsaMina.Commands.Games.Belote;
using ElsaMina.Commands.Games.Cards;
using ElsaMina.Commands.Games.Poker;
using ElsaMina.Commands.Games.President;
using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Rules;

/// <summary>
/// Two orderings that are easy to break and hard to notice: the order a deck is built in, which every
/// deal is dealt from, and the order a hand is sorted into, which is what players read off their page.
/// </summary>
[TestFixture]
public class CardOrderingTest
{
    [Test]
    public void Test_TarotDeck_ShouldRunTheFourSuitsThenTheTrumpsThenTheExcuse()
    {
        var deck = TarotConstants.BuildDeck();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deck[0], Is.EqualTo(TarotCard.Suited(Suit.Hearts, 1)));
            Assert.That(deck[13], Is.EqualTo(TarotCard.Suited(Suit.Hearts, TarotCard.KING)));
            Assert.That(deck[14], Is.EqualTo(TarotCard.Suited(Suit.Spades, 1)));
            Assert.That(deck[28], Is.EqualTo(TarotCard.Suited(Suit.Diamonds, 1)));
            Assert.That(deck[42], Is.EqualTo(TarotCard.Suited(Suit.Clubs, 1)));
            Assert.That(deck[56], Is.EqualTo(TarotCard.Trump(TarotCard.PETIT)));
            Assert.That(deck[76], Is.EqualTo(TarotCard.Trump(TarotCard.MONDE)));
            Assert.That(deck[77], Is.EqualTo(TarotCard.Excuse));
        }
    }

    [Test]
    public void Test_BeloteDeck_ShouldRunTheFourSuitsFromSevenToAce()
    {
        var deck = BeloteConstants.BuildDeck();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deck[0], Is.EqualTo(new BeloteCard(Suit.Hearts, 7)));
            Assert.That(deck[7], Is.EqualTo(new BeloteCard(Suit.Hearts, BeloteCard.ACE)));
            Assert.That(deck[8], Is.EqualTo(new BeloteCard(Suit.Spades, 7)));
            Assert.That(deck[16], Is.EqualTo(new BeloteCard(Suit.Diamonds, 7)));
            Assert.That(deck[24], Is.EqualTo(new BeloteCard(Suit.Clubs, 7)));
        }
    }

    [Test]
    public void Test_PresidentDeck_ShouldRunTheFourSuitsFromThreeToTwo()
    {
        var deck = PresidentConstants.BuildDeck();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deck[0], Is.EqualTo(new PresidentCard(Suit.Hearts, 3)));
            Assert.That(deck[12], Is.EqualTo(new PresidentCard(Suit.Hearts, PresidentCard.TWO)));
            Assert.That(deck[13], Is.EqualTo(new PresidentCard(Suit.Spades, 3)));
            Assert.That(deck[39], Is.EqualTo(new PresidentCard(Suit.Clubs, 3)));
        }
    }

    /// <summary>
    /// Poker builds its deck in a different suit order from the French games, and the deal depends on
    /// it, so the order is pinned rather than inherited from the enum.
    /// </summary>
    [Test]
    public void Test_PokerDeck_ShouldRunClubsDiamondsHeartsSpadesFromTwoToAce()
    {
        var deck = PokerConstants.BuildDeck();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deck[0], Is.EqualTo(new PokerCard(Suit.Clubs, 2)));
            Assert.That(deck[12], Is.EqualTo(new PokerCard(Suit.Clubs, PokerCard.ACE)));
            Assert.That(deck[13], Is.EqualTo(new PokerCard(Suit.Diamonds, 2)));
            Assert.That(deck[26], Is.EqualTo(new PokerCard(Suit.Hearts, 2)));
            Assert.That(deck[39], Is.EqualTo(new PokerCard(Suit.Spades, 2)));
            Assert.That(deck[51], Is.EqualTo(new PokerCard(Suit.Spades, PokerCard.ACE)));
        }
    }

    /// <summary>
    /// A tarot hand reads left to right as hearts, spades, diamonds, clubs, then the trumps in rank
    /// order, and the Excuse last.
    /// </summary>
    [Test]
    public async Task Test_TarotHand_ShouldBeSortedSuitsThenTrumpsThenExcuse()
    {
        var recorder = new GameInteractionRecorder();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("ElsaMina");
        configuration.Trigger.Returns("-");

        var game = new TarotGame(Substitute.For<IRandomService>(), recorder.TemplatesManager, configuration,
            Substitute.For<ITarotStatsService>());
        game.Context = recorder.Context;

        var users = GameUsers.Players(4);
        foreach (var user in users)
        {
            await game.JoinAsync(user);
        }

        await game.StartAsync(users[0]);

        // Taking on a petite folds the dog into the taker's hand and re-sorts it. In the deterministic
        // deal the dog holds the highest trumps and the Excuse, so the whole ordering shows up at once.
        await game.BidAsync(game.CurrentPlayer.User, TarotBid.Petite);
        foreach (var _ in Enumerable.Range(1, 3))
        {
            await game.BidAsync(game.CurrentPlayer.User, TarotBid.Pass);
        }

        var hand = game.Taker.Hand;
        var keys = hand.Select(card => (Kind: (int)card.Kind, Suit: (int?)card.Suit, card.Rank)).ToList();
        await game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hand.Any(card => card.Kind == TarotCardKind.Suited), Is.True);
            Assert.That(hand.Any(card => card.IsTrump), Is.True);
            Assert.That(hand, Does.Contain(TarotCard.Excuse));
            Assert.That(keys, Is.Ordered);
            Assert.That(hand[0].Suit, Is.EqualTo((Suit?)Suit.Hearts));
            Assert.That(hand[^1], Is.EqualTo(TarotCard.Excuse));
        }
    }
}
