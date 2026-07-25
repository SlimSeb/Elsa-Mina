using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Tarot;

/// <summary>
/// Every action goes through a single lock, so two commands arriving at once are applied one after the
/// other and the second one sees the state the first wrote. Panel buttons make this easy to trigger in
/// production: a double click sends the same command twice within milliseconds.
/// </summary>
[TestFixture]
public class TarotConcurrencyTest
{
    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private ITarotStatsService _statsService;
    private TarotGame _game;
    private IReadOnlyList<IUser> _users;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>();
        _configuration = Substitute.For<IConfiguration>();
        _statsService = Substitute.For<ITarotStatsService>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");

        _game = new TarotGame(_randomService, _recorder.TemplatesManager, _configuration, _statsService);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("tarot", _game.GameId);
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    [Test]
    public async Task Test_TwoRacingBids_ShouldOnlyAdvanceTheTurnOnce()
    {
        await StartDealAsync();
        var bidder = _game.CurrentPlayer;

        await Task.WhenAll(
            _game.BidAsync(bidder.User, TarotBid.Petite),
            _game.BidAsync(bidder.User, TarotBid.Petite));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bidder.HasBid, Is.True);
            Assert.That(_game.HighestBid, Is.EqualTo(TarotBid.Petite));
            // The second bid arrives after the first has already moved the turn on, so it is ignored.
            Assert.That(_game.CurrentPlayer, Is.SameAs(_game.Players[1]));
            Assert.That(_game.Players.Count(player => player.HasBid), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_TwoRacingPlays_ShouldOnlyRemoveOneCardFromTheHand()
    {
        await StartDealAsync();
        await BidInOrderAsync(TarotBid.GardeSans, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);

        var leader = _game.CurrentPlayer;
        var handSizeBefore = leader.Hand.Count;
        var card = _game.GetLegalMoves(leader).First();

        await Task.WhenAll(
            _game.PlayAsync(leader.User, card),
            _game.PlayAsync(leader.User, card));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(leader.Hand, Has.Count.EqualTo(handSizeBefore - 1));
            Assert.That(leader.Hand, Does.Not.Contain(card));
            Assert.That(_game.CurrentTrick.Plays, Has.Count.EqualTo(1));
        }
    }

    /// <summary>
    /// Two different players clicking at the same moment must not both get their card in: only the one
    /// whose turn it actually is may play.
    /// </summary>
    [Test]
    public async Task Test_RacingPlaysFromTwoPlayers_ShouldOnlyAcceptTheOneOnTurn()
    {
        await StartDealAsync();
        await BidInOrderAsync(TarotBid.GardeSans, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);

        var leader = _game.CurrentPlayer;
        var waiting = _game.Players.First(player => player != leader);
        var leaderCard = _game.GetLegalMoves(leader).First();
        var waitingCard = waiting.Hand.First();

        await Task.WhenAll(
            _game.PlayAsync(waiting.User, waitingCard),
            _game.PlayAsync(leader.User, leaderCard));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.CurrentTrick.Plays, Has.Count.EqualTo(1));
            Assert.That(_game.CurrentTrick.Plays[0].Player, Is.SameAs(leader));
            Assert.That(waiting.Hand, Does.Contain(waitingCard));
        }
    }

    [Test]
    public async Task Test_RacingSubRequests_ShouldLeaveExactlyOnePendingRequest()
    {
        await StartDealAsync();

        await Task.WhenAll(
            _game.RequestSubAsync(_users[1]),
            _game.RequestSubAsync(_users[2]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Players[1].WantsSub, Is.True);
            Assert.That(_game.Players[2].WantsSub, Is.True);
            Assert.That(_recorder.EntriesOfKind("reply"), Is.All.EqualTo("reply tarot_sub_requested"));
        }
    }

    /// <summary>
    /// Two users racing to fill the same seat: the second sees the seat already taken and is turned away.
    /// </summary>
    [Test]
    public async Task Test_RacingSubAccepts_ShouldOnlyHandTheSeatOverOnce()
    {
        await StartDealAsync();
        await _game.RequestSubAsync(_users[1]);

        var results = await Task.WhenAll(
            _game.AcceptSubAsync(GameUsers.User("substitute1"), "player2"),
            _game.AcceptSubAsync(GameUsers.User("substitute2"), "player2"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.Count(result => result.Success), Is.EqualTo(1));
            Assert.That(results.Single(result => !result.Success).MessageKey,
                Is.EqualTo("tarot_sub_none_pending"));
            Assert.That(_game.Players[1].UserId, Does.StartWith("substitute"));
            Assert.That(_game.Players.Select(player => player.UserId).Distinct().Count(), Is.EqualTo(4));
        }
    }

    private async Task StartDealAsync()
    {
        _users = GameUsers.Players(4);
        foreach (var user in _users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(_users[0]);
    }

    private async Task BidInOrderAsync(params TarotBid[] bids)
    {
        foreach (var bid in bids)
        {
            await _game.BidAsync(_game.CurrentPlayer.User, bid);
        }
    }
}
