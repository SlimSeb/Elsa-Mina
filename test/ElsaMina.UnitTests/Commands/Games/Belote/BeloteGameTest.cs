using System.Globalization;
using ElsaMina.Commands.Games.Belote;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Belote;

[TestFixture]
public class BeloteGameTest
{
    private IRandomService _randomService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IBeloteStatsService _statsService;
    private IContext _context;
    private BeloteGame _game;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>(); // ShuffleInPlace is a no-op -> deterministic deal
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _statsService = Substitute.For<IBeloteStatsService>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");
        _context.RoomId.Returns("testroom");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        _game = new BeloteGame(_randomService, _templatesManager, _configuration, _statsService);
        _game.Context = _context;
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    private static IUser User(string id)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return user;
    }

    private async Task<IReadOnlyList<IUser>> JoinAndStartAsync()
    {
        var users = Enumerable.Range(1, 4).Select(index => User($"player{index}")).ToList();
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        return users;
    }

    private async Task PassAsync(int times)
    {
        for (var i = 0; i < times; i++)
        {
            await _game.BidAsync(_game.CurrentPlayer.User, pass: true, null);
        }
    }

    [Test]
    public async Task Test_Start_ShouldDealFiveCardsAndTurnCard()
    {
        await JoinAndStartAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(BelotePhase.Bidding));
            Assert.That(_game.BiddingRound, Is.EqualTo(1));
            Assert.That(_game.Players, Has.Count.EqualTo(4));
            Assert.That(_game.Players.All(player => player.Hand.Count == 5), Is.True);
            Assert.That(_game.TurnedCard, Is.Not.Null);
            Assert.That(_game.Trump, Is.Null);
        }
    }

    [Test]
    public async Task Test_Take_ShouldCompleteHandsAndStartPlaying()
    {
        var users = await JoinAndStartAsync();

        await _game.BidAsync(_game.CurrentPlayer.User, pass: false, null); // player1 takes the turned suit

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(BelotePhase.Playing));
            Assert.That(_game.Taker?.UserId, Is.EqualTo(users[0].UserId));
            Assert.That(_game.Trump, Is.EqualTo(BeloteSuit.Diamonds)); // turned card is the Jack of Diamonds
            Assert.That(_game.Players.All(player => player.Hand.Count == 8), Is.True);
        }
    }

    [Test]
    public async Task Test_Take_ShouldAssignTeamsAcrossTheTable()
    {
        await JoinAndStartAsync();

        await _game.BidAsync(_game.CurrentPlayer.User, pass: false, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Players[0].Team, Is.EqualTo(0));
            Assert.That(_game.Players[1].Team, Is.EqualTo(1));
            Assert.That(_game.Players[2].Team, Is.EqualTo(0));
            Assert.That(_game.Players[3].Team, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_Take_ShouldDetectBelote_WhenTakerHoldsTrumpKingAndQueen()
    {
        await JoinAndStartAsync();

        // With the deterministic deal the taker ends up with both the King and Queen of Diamonds (trump).
        await _game.BidAsync(_game.CurrentPlayer.User, pass: false, null);

        Assert.That(_game.Players[0].HasBelote, Is.True);
    }

    [Test]
    public async Task Test_Bidding_ShouldStartSecondRound_WhenEveryonePassesOnce()
    {
        await JoinAndStartAsync();

        await PassAsync(4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(BelotePhase.Bidding));
            Assert.That(_game.BiddingRound, Is.EqualTo(2));
            Assert.That(_game.Players.All(player => !player.HasBid), Is.True);
        }
    }

    [Test]
    public async Task Test_Bidding_ShouldEndGame_WhenEveryonePassesTwice()
    {
        await JoinAndStartAsync();

        await PassAsync(8);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsEnded, Is.True);
            Assert.That(_game.Taker, Is.Null);
        }

        _context.Received().ReplyLocalizedMessage("belote_bidding_all_passed");
    }

    [Test]
    public async Task Test_Bidding_ShouldTakeChosenSuit_InSecondRound()
    {
        var users = await JoinAndStartAsync();
        await PassAsync(4);

        await _game.BidAsync(_game.CurrentPlayer.User, pass: false, BeloteSuit.Hearts);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(BelotePhase.Playing));
            Assert.That(_game.Trump, Is.EqualTo(BeloteSuit.Hearts));
            Assert.That(_game.Taker?.UserId, Is.EqualTo(users[0].UserId));
        }
    }

    [Test]
    public async Task Test_Bidding_ShouldRejectTurnedSuit_InSecondRound()
    {
        await JoinAndStartAsync();
        await PassAsync(4);

        await _game.BidAsync(_game.CurrentPlayer.User, pass: false, BeloteSuit.Diamonds);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(BelotePhase.Bidding));
            Assert.That(_game.Taker, Is.Null);
        }

        _context.Received().ReplyLocalizedMessage("belote_bid_suit_forbidden");
    }

    [Test]
    public async Task Test_Cancel_ShouldRenderCancelledPanel_WhenInLobby()
    {
        await _game.BeginJoinPhaseAsync();
        await _game.JoinAsync(User("player1"));
        _templatesManager.ClearReceivedCalls();
        _context.ClearReceivedCalls();

        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(BelotePhase.Finished));
            await _templatesManager.Received(1)
                .GetTemplateAsync("Games/Belote/BeloteCancelled", Arg.Any<object>());
            _context.Received(1).SendUpdatableHtml($"belote-{_game.GameId}", Arg.Any<string>(), true);
        }
    }

    [Test]
    public async Task Test_Cancel_ShouldNotRenderCancelledPanel_WhenAlreadyPlaying()
    {
        await JoinAndStartAsync();
        _templatesManager.ClearReceivedCalls();

        await _game.CancelAsync();

        await _templatesManager.DidNotReceive()
            .GetTemplateAsync("Games/Belote/BeloteCancelled", Arg.Any<object>());
    }

    [Test]
    public async Task Test_RequestSub_ShouldMarkPlayer_WhenPlayerRequests()
    {
        var users = await JoinAndStartAsync();

        var (success, _, _) = await _game.RequestSubAsync(users[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players.Single(player => player.UserId == users[1].UserId).WantsSub, Is.True);
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldReplacePlayerAndKeepHand()
    {
        var users = await JoinAndStartAsync();
        var leaving = _game.Players[1];
        var handBefore = leaving.Hand.ToList();
        await _game.RequestSubAsync(users[1]);

        var newUser = User("substitute");
        var (success, _, _) = await _game.AcceptSubAsync(newUser, users[1].UserId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players[1].UserId, Is.EqualTo("substitute"));
            Assert.That(_game.Players[1].Hand, Is.EquivalentTo(handBefore));
            Assert.That(_game.Players.Any(player => player.UserId == users[1].UserId), Is.False);
        }
    }

    [Test]
    public async Task Test_GetLegalMoves_ShouldReturnWholeHand_WhenLeading()
    {
        await ReachPlayingAsync();
        var hand = new[] { Card("7h"), Card("10s") };
        var player = MakePlayerWithHand(0, hand);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo(hand));
    }

    [Test]
    public async Task Test_GetLegalMoves_ShouldForceFollowingSuit()
    {
        await ReachPlayingAsync();
        _game.CurrentTrick.Add(MakePlayer("a", 1), Card("7h"));
        var player = MakePlayerWithHand(0, [Card("ah"), Card("10s"), Card("8d")]);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo([Card("ah")]));
    }

    [Test]
    public async Task Test_GetLegalMoves_ShouldForceTrumping_WhenCannotFollowAndPartnerNotWinning()
    {
        await ReachPlayingAsync();
        _game.CurrentTrick.Add(MakePlayer("a", 1), Card("7h"));
        var player = MakePlayerWithHand(0, [Card("10s"), Card("8d"), Card("9d")]);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo([Card("8d"), Card("9d")]));
    }

    [Test]
    public async Task Test_GetLegalMoves_ShouldAllowDiscard_WhenPartnerIsWinning()
    {
        await ReachPlayingAsync();
        _game.CurrentTrick.Add(MakePlayer("a", 0), Card("7h")); // partner leads and is master
        var hand = new[] { Card("10s"), Card("8d") };
        var player = MakePlayerWithHand(0, hand);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo(hand));
    }

    [Test]
    public async Task Test_GetLegalMoves_ShouldForceOvertrumping_WhenAble()
    {
        await ReachPlayingAsync();
        _game.CurrentTrick.Add(MakePlayer("a", 1), Card("7h"));
        _game.CurrentTrick.Add(MakePlayer("b", 1), Card("10d")); // opponent already cut with the 10 of trump
        var player = MakePlayerWithHand(0, [Card("jd"), Card("7d"), Card("10s")]);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo([Card("jd")]));
    }

    [Test]
    public async Task Test_GetLegalMoves_ShouldAllowAnyTrump_WhenCannotOvertrump()
    {
        await ReachPlayingAsync();
        _game.CurrentTrick.Add(MakePlayer("a", 1), Card("7h"));
        _game.CurrentTrick.Add(MakePlayer("b", 1), Card("10d"));
        var player = MakePlayerWithHand(0, [Card("7d"), Card("8d"), Card("10s")]);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo([Card("7d"), Card("8d")]));
    }

    [Test]
    public async Task Test_GetLegalMoves_ShouldForceOvertrumping_WhenTrumpIsLed()
    {
        await ReachPlayingAsync();
        _game.CurrentTrick.Add(MakePlayer("a", 1), Card("9d")); // trump lead
        var player = MakePlayerWithHand(0, [Card("jd"), Card("7d"), Card("10s")]);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo([Card("jd")]));
    }

    private async Task ReachPlayingAsync()
    {
        await JoinAndStartAsync();
        await _game.BidAsync(_game.CurrentPlayer.User, pass: false, null); // trump becomes Diamonds
    }

    private static BeloteCard Card(string token) => BeloteCard.Parse(token);

    private static BelotePlayer MakePlayer(string id, int team) => new(User(id)) { Team = team };

    private static BelotePlayer MakePlayerWithHand(int team, IEnumerable<BeloteCard> cards)
    {
        var player = MakePlayer("x", team);
        player.Hand.AddRange(cards);
        return player;
    }
}