using ElsaMina.Commands.Games.Belote;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Belote;

/// <summary>
/// Pins the turn timer and the action lock of belote: the fallback action for each phase, the warning
/// PM, the timer stopping once the deal is over, and two commands arriving at once being serialized.
/// </summary>
[TestFixture]
public class BeloteTurnTimerAndConcurrencyTest
{
    private static readonly TimeSpan SHORT_TURN_TIMEOUT = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan WARNING_ONLY_TURN_TIMEOUT =
        BeloteConstants.TURN_TIMEOUT_WARNING_REMAINING + TimeSpan.FromMilliseconds(200);

    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private IBeloteStatsService _statsService;
    private BeloteGame _game;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>();
        _configuration = Substitute.For<IConfiguration>();
        _statsService = Substitute.For<IBeloteStatsService>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_game is not null)
        {
            await _game.CancelAsync();
        }
    }

    [Test]
    public async Task Test_Timeout_ShouldPassForThePlayerWhoRanOutOfTime_WhileBidding()
    {
        await StartDealAsync(SHORT_TURN_TIMEOUT);

        await Wait.UntilAsync(() => _game.Players[0].HasBid, "the first bidder to be passed automatically");
        var currentPlayer = _game.CurrentPlayer;
        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Taker, Is.Null);
            Assert.That(currentPlayer, Is.Not.SameAs(_game.Players[0]));
        }
    }

    [Test]
    public async Task Test_Timeout_ShouldPlayTheFirstLegalMove_WhilePlaying()
    {
        await StartDealAsync(SHORT_TURN_TIMEOUT);
        await _game.BidAsync(_game.CurrentPlayer.User, pass: false, null);
        var leader = _game.CurrentPlayer;
        var expectedCard = _game.GetLegalMoves(leader).First();

        await Wait.UntilAsync(() => _game.CurrentTrick.Plays.Count > 0, "the leader to be played for");
        var firstPlay = _game.CurrentTrick.Plays[0];
        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPlay.Player, Is.SameAs(leader));
            Assert.That(firstPlay.Card, Is.EqualTo(expectedCard));
        }
    }

    [Test]
    public async Task Test_TurnWarning_ShouldBeSentByPrivateMessageToTheActivePlayer()
    {
        await StartDealAsync(WARNING_ONLY_TURN_TIMEOUT);

        await Wait.UntilAsync(() => _recorder.EntriesOfKind("say").Count > 0, "the turn warning PM");
        var warnings = _recorder.EntriesOfKind("say");
        await _game.CancelAsync();

        Assert.That(warnings, Is.EqualTo(new[] { "say /pm player1, belote_turn_timeout_warning" }));
    }

    [Test]
    public async Task Test_TurnTimer_ShouldStopOnceTheDealIsFinished()
    {
        await StartDealAsync(SHORT_TURN_TIMEOUT);
        await _game.CancelAsync();
        _recorder.Clear();

        await Wait.ForQuietPeriodAsync(SHORT_TURN_TIMEOUT * 4);

        Assert.That(_recorder.Entries, Is.Empty);
    }

    [Test]
    public async Task Test_TwoRacingBids_ShouldOnlyTakeTheContractOnce()
    {
        await StartDealAsync(BeloteConstants.TURN_TIMEOUT);
        var bidder = _game.CurrentPlayer;

        await Task.WhenAll(
            _game.BidAsync(bidder.User, pass: false, null),
            _game.BidAsync(bidder.User, pass: false, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Taker, Is.SameAs(bidder));
            Assert.That(_game.Players.Count(player => player.IsTaker), Is.EqualTo(1));
            Assert.That(_recorder.EntriesOfKind("reply").Count(entry => entry == "reply belote_taker_announced"),
                Is.EqualTo(1));
            Assert.That(_game.Players.All(player => player.Hand.Count == BeloteConstants.HAND_SIZE), Is.True);
        }
    }

    [Test]
    public async Task Test_TwoRacingPlays_ShouldOnlyRemoveOneCardFromTheHand()
    {
        await StartDealAsync(BeloteConstants.TURN_TIMEOUT);
        await _game.BidAsync(_game.CurrentPlayer.User, pass: false, null);

        var leader = _game.CurrentPlayer;
        var handSizeBefore = leader.Hand.Count;
        var card = _game.GetLegalMoves(leader).First();

        await Task.WhenAll(
            _game.PlayAsync(leader.User, card),
            _game.PlayAsync(leader.User, card));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(leader.Hand, Has.Count.EqualTo(handSizeBefore - 1));
            Assert.That(_game.CurrentTrick.Plays, Has.Count.EqualTo(1));
        }
    }

    private async Task<IReadOnlyList<IUser>> StartDealAsync(TimeSpan turnTimeout)
    {
        _game = new BeloteGame(_randomService, _recorder.TemplatesManager, _configuration, _statsService,
            turnTimeout);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("belote", _game.GameId);

        var users = GameUsers.Players(BeloteConstants.PLAYER_COUNT);
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        return users;
    }
}
