using System.Globalization;
using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class TarotGameTest
{
    private static readonly string[] LOW_HEART_DISCARDS = ["2h", "3h", "4h", "5h", "6h", "7h"];
    private static readonly string[] DISCARDS_INCLUDING_KING = ["kh", "2h", "3h", "4h", "5h", "6h"];

    private IRandomService _randomService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private ITarotStatsService _statsService;
    private IContext _context;
    private TarotGame _game;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>(); // ShuffleInPlace is a no-op -> deterministic deal
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _statsService = Substitute.For<ITarotStatsService>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");
        _context.RoomId.Returns("testroom");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        _game = new TarotGame(_randomService, _templatesManager, _configuration, _statsService);
        _game.Context = _context;
    }

    [TearDown]
    public void TearDown() => _game.Cancel();

    private static IUser User(string id)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return user;
    }

    private async Task<IReadOnlyList<IUser>> JoinAndStartAsync(int count)
    {
        var users = Enumerable.Range(1, count).Select(index => User($"player{index}")).ToList();
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        return users;
    }

    private async Task BidInOrderAsync(params TarotBid[] bids)
    {
        foreach (var bid in bids)
        {
            await _game.BidAsync(_game.CurrentPlayer.User, bid);
        }
    }

    [TestCase(3, 24, 6)]
    [TestCase(4, 18, 6)]
    [TestCase(5, 15, 3)]
    public async Task Test_Start_ShouldDealCorrectHandAndDogSizes(int players, int handSize, int dogSize)
    {
        await JoinAndStartAsync(players);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Bidding));
            Assert.That(_game.Players, Has.Count.EqualTo(players));
            Assert.That(_game.Players.All(player => player.Hand.Count == handSize), Is.True);
            Assert.That(_game.Dog, Has.Count.EqualTo(dogSize));
        }
    }

    [Test]
    public async Task Test_Bidding_ShouldEndGame_WhenEveryonePasses()
    {
        await JoinAndStartAsync(4);

        await BidInOrderAsync(TarotBid.Pass, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsEnded, Is.True);
            Assert.That(_game.Taker, Is.Null);
        }

        _context.Received().ReplyLocalizedMessage("tarot_bidding_all_passed");
    }

    [Test]
    public async Task Test_Bidding_ShouldSetTakerAndEnterDiscard_WhenPetiteWins()
    {
        var users = await JoinAndStartAsync(4);

        await BidInOrderAsync(TarotBid.Petite, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Taker?.UserId, Is.EqualTo(users[0].UserId));
            Assert.That(_game.HighestBid, Is.EqualTo(TarotBid.Petite));
            Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Discard));
            Assert.That(_game.DogRevealed, Is.True);
            Assert.That(_game.Taker.Hand, Has.Count.EqualTo(24)); // 18 + 6 dog cards
        }
    }

    [Test]
    public async Task Test_GardeSans_ShouldSkipDiscardAndKeepDogForTaker()
    {
        await JoinAndStartAsync(4);

        await BidInOrderAsync(TarotBid.GardeSans, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Playing));
            Assert.That(_game.Taker.Hand, Has.Count.EqualTo(18));
            Assert.That(_game.Taker.CapturedPile, Has.Count.EqualTo(6)); // the dog
        }
    }

    [Test]
    public async Task Test_Discard_ShouldMoveCardsToCapturedPileAndStartPlaying()
    {
        await JoinAndStartAsync(4);
        await BidInOrderAsync(TarotBid.Petite, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);

        // player1 holds all hearts after the deterministic deal; discard six low hearts.
        var discards = LOW_HEART_DISCARDS.Select(TarotCard.Parse).ToList();
        await _game.DiscardAsync(_game.Taker.User, discards);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Playing));
            Assert.That(_game.Taker.Hand, Has.Count.EqualTo(18));
            Assert.That(_game.Taker.CapturedPile, Has.Count.EqualTo(6));
        }
    }

    [Test]
    public async Task Test_Discard_ShouldBeRejected_WhenDiscardingForbiddenCards()
    {
        await JoinAndStartAsync(4);
        await BidInOrderAsync(TarotBid.Petite, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);

        // Includes the king of hearts, which can never be discarded.
        var discards = DISCARDS_INCLUDING_KING.Select(TarotCard.Parse).ToList();
        await _game.DiscardAsync(_game.Taker.User, discards);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Discard));
            Assert.That(_game.Taker.Hand, Has.Count.EqualTo(24));
        }

        _context.Received().ReplyLocalizedMessage("tarot_discard_forbidden_card");
    }

    [Test]
    public async Task Test_RequestSub_ShouldFail_WhenInLobby()
    {
        await _game.JoinAsync(User("player1"));

        var (success, messageKey, _) = await _game.RequestSubAsync(User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_not_active"));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldFail_WhenNotAPlayer()
    {
        await JoinAndStartAsync(4);

        var (success, messageKey, _) = await _game.RequestSubAsync(User("stranger"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_not_a_player"));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldMarkPlayer_WhenPlayerRequests()
    {
        var users = await JoinAndStartAsync(4);

        var (success, _, _) = await _game.RequestSubAsync(users[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players.Single(player => player.UserId == users[1].UserId).WantsSub, Is.True);
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldToggleOff_WhenRequestedTwice()
    {
        var users = await JoinAndStartAsync(4);

        await _game.RequestSubAsync(users[1]);
        await _game.RequestSubAsync(users[1]);

        Assert.That(_game.Players.Single(player => player.UserId == users[1].UserId).WantsSub, Is.False);
    }

    [Test]
    public async Task Test_AcceptSub_ShouldReplacePlayerAndKeepHand()
    {
        var users = await JoinAndStartAsync(4);
        var leaving = _game.Players[1];
        var handBefore = leaving.Hand.ToList();
        await _game.RequestSubAsync(users[1]);

        var newUser = User("substitute");
        var (success, _, _) = await _game.AcceptSubAsync(newUser, users[1].UserId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players[1].UserId, Is.EqualTo("substitute"));
            Assert.That(_game.Players[1].WantsSub, Is.False);
            Assert.That(_game.Players[1].Hand, Is.EquivalentTo(handBefore));
            Assert.That(_game.Players.Any(player => player.UserId == users[1].UserId), Is.False);
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldFail_WhenAlreadyPlayer()
    {
        var users = await JoinAndStartAsync(4);
        await _game.RequestSubAsync(users[1]);

        var (success, messageKey, _) = await _game.AcceptSubAsync(users[0], users[1].UserId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_already_player"));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldFail_WhenNonePending()
    {
        await JoinAndStartAsync(4);

        var (success, messageKey, _) = await _game.AcceptSubAsync(User("substitute"), null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_none_pending"));
        }
    }

    [Test]
    public void Test_GetLegalMoves_ShouldReturnWholeHand_WhenLeading()
    {
        var hand = new[] { Card("3h"), Card("10s") };
        var player = MakePlayerWithHand(hand);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo(hand));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldForceFollowingSuit()
    {
        LeadTrickWith("a", Card("5h"));
        var player = MakePlayerWithHand([Card("3h"), Card("10s"), Card("t5")]);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo([Card("3h")]));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldForceTrumping_WhenNoLeadSuit()
    {
        LeadTrickWith("a", Card("5h"));
        var player = MakePlayerWithHand([Card("10s"), Card("t5"), Card("t8")]);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo([Card("t5"), Card("t8")]));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldForceOvertrumping_WhenAble()
    {
        LeadTrickWith("a", Card("5h"));
        _game.CurrentTrick.Add(MakePlayerWithHand([]), Card("t10"));
        var player = MakePlayerWithHand([Card("t5"), Card("t12"), Card("3s")]);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo([Card("t12")]));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldAllowAnyTrump_WhenCannotOvertrump()
    {
        LeadTrickWith("a", Card("5h"));
        _game.CurrentTrick.Add(MakePlayerWithHand([]), Card("t10"));
        var player = MakePlayerWithHand([Card("t5"), Card("t8"), Card("3s")]);

        Assert.That(_game.GetLegalMoves(player), Is.EquivalentTo([Card("t5"), Card("t8")]));
    }

    [Test]
    public void Test_GetLegalMoves_ShouldAlwaysAllowTheExcuse()
    {
        LeadTrickWith("a", Card("5h"));
        var player = MakePlayerWithHand([Card("3h"), Card("exc")]);

        Assert.That(_game.GetLegalMoves(player), Does.Contain(Card("exc")));
    }

    private static TarotCard Card(string token) => TarotCard.Parse(token);

    private static TarotPlayer MakePlayerWithHand(IEnumerable<TarotCard> cards)
    {
        var player = new TarotPlayer(User("x"));
        player.Hand.AddRange(cards);
        return player;
    }

    private void LeadTrickWith(string playerId, TarotCard card)
    {
        _game.CurrentTrick.Add(new TarotPlayer(User(playerId)), card);
    }
}
