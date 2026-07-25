using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Tarot;

/// <summary>
/// Pins the turn timer: which action a phase falls back to when a player runs out of time, the warning
/// sent by PM shortly before that, and the fact that the timer stops once the deal leaves a phase that
/// accepts actions.
/// </summary>
[TestFixture]
public class TarotTurnTimerTest
{
    private static readonly TimeSpan SHORT_TURN_TIMEOUT = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The warning fires <see cref="TarotConstants.TURN_TIMEOUT_WARNING_REMAINING"/> before the turn
    /// runs out, so a turn barely longer than that threshold warns almost immediately and then leaves
    /// plenty of time before the auto-action would fire.
    /// </summary>
    private static readonly TimeSpan WARNING_ONLY_TURN_TIMEOUT =
        TarotConstants.TURN_TIMEOUT_WARNING_REMAINING + TimeSpan.FromMilliseconds(200);

    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private ITarotStatsService _statsService;
    private TarotGame _game;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>();
        _configuration = Substitute.For<IConfiguration>();
        _statsService = Substitute.For<ITarotStatsService>();

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
        var users = await StartDealAsync(SHORT_TURN_TIMEOUT);

        await Wait.UntilAsync(() => _game.Players[0].HasBid, "the first bidder to be passed automatically");
        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Players[0].UserId, Is.EqualTo(users[0].UserId));
            Assert.That(_game.Players[0].Bid, Is.EqualTo(TarotBid.Pass));
            Assert.That(_game.HighestBid, Is.EqualTo(TarotBid.Pass));
        }
    }

    [Test]
    public async Task Test_Timeout_ShouldPlayTheFirstLegalMove_WhilePlaying()
    {
        await StartDealAsync(SHORT_TURN_TIMEOUT);
        await BidToPlayingPhaseAsync();
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
    public async Task Test_Timeout_ShouldDiscardAutomatically_WhileDiscarding()
    {
        await StartDealAsync(SHORT_TURN_TIMEOUT);
        await BidInOrderAsync(TarotBid.Petite, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);
        Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Discard));

        await Wait.UntilAsync(() => _game.Phase != TarotPhase.Discard, "the discard to be resolved");
        var taker = _game.Taker;
        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(taker.Hand, Has.Count.EqualTo(18));
            Assert.That(taker.CapturedPile, Has.Count.EqualTo(6));
        }
    }

    [Test]
    public async Task Test_TurnWarning_ShouldBeSentByPrivateMessageToTheActivePlayer()
    {
        await StartDealAsync(WARNING_ONLY_TURN_TIMEOUT);

        await Wait.UntilAsync(() => _recorder.EntriesOfKind("say").Count > 0, "the turn warning PM");
        var warnings = _recorder.EntriesOfKind("say");
        await _game.CancelAsync();

        Assert.That(warnings, Is.EqualTo(new[] { "say /pm player1, tarot_turn_timeout_warning" }));
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

    /// <summary>
    /// The timer only ever runs during a phase that accepts actions, so a game still gathering players
    /// must never fire an auto-action.
    /// </summary>
    [Test]
    public async Task Test_TurnTimer_ShouldNotRunWhileStillInTheLobby()
    {
        _game = new TarotGame(_randomService, _recorder.TemplatesManager, _configuration, _statsService,
            SHORT_TURN_TIMEOUT);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("tarot", _game.GameId);

        await _game.BeginJoinPhaseAsync();
        await _game.JoinAsync(GameUsers.User("player1"));
        _recorder.Clear();

        await Wait.ForQuietPeriodAsync(SHORT_TURN_TIMEOUT * 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Lobby));
            Assert.That(_recorder.Entries, Is.Empty);
        }
    }

    private async Task<IReadOnlyList<IUser>> StartDealAsync(TimeSpan turnTimeout)
    {
        _game = new TarotGame(_randomService, _recorder.TemplatesManager, _configuration, _statsService,
            turnTimeout);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("tarot", _game.GameId);

        var users = GameUsers.Players(4);
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        return users;
    }

    private async Task BidToPlayingPhaseAsync() =>
        await BidInOrderAsync(TarotBid.GardeSans, TarotBid.Pass, TarotBid.Pass, TarotBid.Pass);

    private async Task BidInOrderAsync(params TarotBid[] bids)
    {
        foreach (var bid in bids)
        {
            await _game.BidAsync(_game.CurrentPlayer.User, bid);
        }
    }
}
