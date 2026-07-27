using ElsaMina.Commands.Games.President;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.President;

/// <summary>
/// Pins the turn timer and the action lock of président. The exchange phase is the interesting case:
/// its timeout resolves every outstanding give-back at once, and its warning goes to every debtor
/// rather than to a single player on turn.
/// </summary>
[TestFixture]
public class PresidentTurnTimerAndConcurrencyTest
{
    /// <summary>
    /// The turn a test lets run out. Long enough that the setup leading up to it always finishes first,
    /// even on a loaded CI runner, since any action taken while the clock ticks restarts it.
    /// </summary>
    private static readonly TimeSpan EXPIRING_TURN_TIMEOUT = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Used by the exchange tests, which have to drive a whole round by hand before the timer matters.
    /// Every action restarts the clock, so this only has to exceed the slowest single action.
    /// </summary>
    private static readonly TimeSpan LENIENT_TURN_TIMEOUT = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Used only where the test asserts that nothing happens, so it can afford to be short.
    /// </summary>
    private static readonly TimeSpan IDLE_TURN_TIMEOUT = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// A turn a little longer than the warning threshold, so the warning fires early in the turn and
    /// the auto-action never gets a chance to.
    /// </summary>
    private static readonly TimeSpan EXPIRING_WARNING_TURN_TIMEOUT =
        PresidentConstants.TURN_TIMEOUT_WARNING_REMAINING + EXPIRING_TURN_TIMEOUT;

    /// <summary>
    /// The same, with enough slack to drive a whole round by hand first.
    /// </summary>
    private static readonly TimeSpan LENIENT_WARNING_TURN_TIMEOUT =
        PresidentConstants.TURN_TIMEOUT_WARNING_REMAINING + LENIENT_TURN_TIMEOUT;

    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private PresidentGame _game;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>();
        _configuration = Substitute.For<IConfiguration>();

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
    public async Task Test_Timeout_ShouldPlayTheFirstLegalCombination_WhenLeading()
    {
        await StartGameAsync(EXPIRING_TURN_TIMEOUT);
        var leader = _game.CurrentPlayer;
        var (expectedRank, expectedCount) = _game.GetLegalPlays(leader)[0];

        await Wait.UntilAsync(() => !_game.CurrentTrick.IsEmpty, "the leader to be played for");
        var firstPlay = _game.CurrentTrick.Plays[0];
        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPlay.Player, Is.SameAs(leader));
            Assert.That(firstPlay.Cards, Has.Count.EqualTo(expectedCount));
            Assert.That(firstPlay.Cards.All(card => card.Rank == expectedRank), Is.True);
        }
    }

    [Test]
    public async Task Test_Timeout_ShouldPassRatherThanPlay_WhenThePileIsNotEmpty()
    {
        await StartGameAsync(EXPIRING_TURN_TIMEOUT);
        var leader = _game.CurrentPlayer;
        var (rank, count) = _game.GetLegalPlays(leader)[0];
        await _game.PlayAsync(leader.User, rank, count);
        var responder = _game.CurrentPlayer;

        await Wait.UntilAsync(() => _game.CurrentPlayer != responder || responder.HasPassed,
            "the responder to be passed automatically");
        var responderPassed = responder.HasPassed;
        await _game.CancelAsync();

        Assert.That(responderPassed, Is.True);
    }

    [Test]
    public async Task Test_TurnWarning_ShouldBeSentByPrivateMessageToTheActivePlayer()
    {
        await StartGameAsync(EXPIRING_WARNING_TURN_TIMEOUT);

        await Wait.UntilAsync(() => _recorder.EntriesOfKind("say").Count > 0, "the turn warning PM");
        var warnings = _recorder.EntriesOfKind("say");
        await _game.CancelAsync();

        Assert.That(warnings, Is.EqualTo(new[] { "say /pm player1, president_turn_timeout_warning" }));
    }

    /// <summary>
    /// The exchange has no single player on turn: when it times out every outstanding give-back is
    /// resolved at once, handing over the debtor's lowest cards, and the round opens straight away.
    /// </summary>
    [Test]
    public async Task Test_Timeout_ShouldResolveEveryOutstandingGiveBack_DuringTheExchange()
    {
        await StartGameAsync(LENIENT_TURN_TIMEOUT, rounds: 2);
        await PlayFirstRoundOutAsync();
        Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Exchange));

        await Wait.UntilAsync(() => _game.Phase == PresidentPhase.Playing, "the exchange to time out");
        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.RoundNumber, Is.EqualTo(2));
            Assert.That(_game.Players.All(player => player.CardsToGive == 0), Is.True);
            Assert.That(_game.Log.Count(entry => entry == "president_exchange_returned"), Is.EqualTo(2));
        }
    }

    /// <summary>
    /// The exchange warning goes to every player who still owes cards, not to a single player on turn.
    /// </summary>
    [Test]
    public async Task Test_TurnWarning_ShouldReachEveryDebtor_DuringTheExchange()
    {
        await StartGameAsync(LENIENT_WARNING_TURN_TIMEOUT, rounds: 2);
        await PlayFirstRoundOutAsync();
        Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Exchange));
        var debtors = _game.Players.Where(player => player.CardsToGive > 0)
            .Select(player => player.UserId)
            .ToList();
        _recorder.Clear();

        await Wait.UntilAsync(() => _recorder.EntriesOfKind("say").Count >= debtors.Count,
            "the exchange warning PMs");
        var warnings = _recorder.EntriesOfKind("say");
        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(debtors, Has.Count.EqualTo(2));
            Assert.That(warnings, Is.EqualTo(debtors
                .Select(debtor => $"say /pm {debtor}, president_turn_timeout_warning")
                .ToList()));
        }
    }

    [Test]
    public async Task Test_TurnTimer_ShouldStopOnceTheGameIsFinished()
    {
        await StartGameAsync(IDLE_TURN_TIMEOUT);
        await _game.CancelAsync();
        _recorder.Clear();

        await Wait.ForQuietPeriodAsync(IDLE_TURN_TIMEOUT * 4);

        Assert.That(_recorder.Entries, Is.Empty);
    }

    [Test]
    public async Task Test_TwoRacingPlays_ShouldOnlyPutOneCombinationOnThePile()
    {
        await StartGameAsync(PresidentConstants.TURN_TIMEOUT);
        var leader = _game.CurrentPlayer;
        var handSizeBefore = leader.Hand.Count;
        var (rank, count) = _game.GetLegalPlays(leader)[0];

        await Task.WhenAll(
            _game.PlayAsync(leader.User, rank, count),
            _game.PlayAsync(leader.User, rank, count));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.CurrentTrick.Plays, Has.Count.EqualTo(1));
            Assert.That(leader.Hand, Has.Count.EqualTo(handSizeBefore - count));
        }
    }

    [Test]
    public async Task Test_RacingPlayAndPass_ShouldOnlyApplyTheFirstToTakeTheLock()
    {
        await StartGameAsync(PresidentConstants.TURN_TIMEOUT);
        var leader = _game.CurrentPlayer;
        var (rank, count) = _game.GetLegalPlays(leader)[0];

        await Task.WhenAll(
            _game.PlayAsync(leader.User, rank, count),
            _game.PassAsync(leader.User));

        using (Assert.EnterMultipleScope())
        {
            // Passing while leading is never legal, so only the play can have gone through.
            Assert.That(_game.CurrentTrick.Plays, Has.Count.EqualTo(1));
            Assert.That(leader.HasPassed, Is.False);
        }
    }

    private async Task<IReadOnlyList<IUser>> StartGameAsync(TimeSpan turnTimeout, int rounds = 1)
    {
        _game = new PresidentGame(_randomService, _recorder.TemplatesManager, _configuration, turnTimeout);
        _game.Context = _recorder.Context;
        _game.TotalRounds = rounds;
        _recorder.MaskGameId("president", _game.GameId);

        var users = GameUsers.Players(4);
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        return users;
    }

    /// <summary>
    /// Empties every hand by always taking the first legal combination or passing, which brings a
    /// two-round game to the exchange that opens its second round.
    /// </summary>
    private async Task PlayFirstRoundOutAsync()
    {
        var safetyLimit = 200;
        while (_game.Phase == PresidentPhase.Playing && _game.RoundNumber == 1 && safetyLimit-- > 0)
        {
            var player = _game.CurrentPlayer;
            if (player is null)
            {
                break;
            }

            var legalPlays = _game.GetLegalPlays(player);
            if (legalPlays.Count > 0)
            {
                var (rank, count) = legalPlays[0];
                await _game.PlayAsync(player.User, rank, count);
                continue;
            }

            await _game.PassAsync(player.User);
        }

        Assert.That(safetyLimit, Is.GreaterThan(0), "the first round did not terminate");
    }
}
