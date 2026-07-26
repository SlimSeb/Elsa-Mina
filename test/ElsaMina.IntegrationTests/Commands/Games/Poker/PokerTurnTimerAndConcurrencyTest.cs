using ElsaMina.Commands.Economy;
using ElsaMina.Commands.Games.Poker;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Poker;

/// <summary>
/// Pins the turn timer and the action lock of poker. Poker has no warning PM, and its fallback action
/// depends on the state of the betting round: a free check is taken, anything that costs chips folds.
/// </summary>
[TestFixture]
public class PokerTurnTimerAndConcurrencyTest
{
    /// <summary>
    /// The turn a test lets run out. Long enough that the setup leading up to it always finishes first,
    /// even on a loaded CI runner, since any action taken while the clock ticks restarts it.
    /// </summary>
    private static readonly TimeSpan EXPIRING_TURN_TIMEOUT = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Used only where the test asserts that nothing happens, so it can afford to be short.
    /// </summary>
    private static readonly TimeSpan IDLE_TURN_TIMEOUT = TimeSpan.FromMilliseconds(200);

    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private IMoneyService _moneyService;
    private PokerGame _game;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>();
        _configuration = Substitute.For<IConfiguration>();
        _moneyService = Substitute.For<IMoneyService>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");
        _moneyService.GetBalanceAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(1000L));
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_game is not null)
        {
            await _game.CancelAsync();
        }
    }

    /// <summary>
    /// Preflop the first actor still owes the big blind, so timing out costs them the hand.
    /// </summary>
    [Test]
    public async Task Test_Timeout_ShouldFold_WhenThereIsSomethingToCall()
    {
        await StartHandAsync(EXPIRING_TURN_TIMEOUT);
        var actor = _game.CurrentPlayer;
        Assert.That(_game.AmountToCall(actor), Is.GreaterThan(0));

        await Wait.UntilAsync(() => actor.HasFolded, "the player on turn to be folded automatically");
        await _game.CancelAsync();

        Assert.That(actor.HasFolded, Is.True);
    }

    /// <summary>
    /// Once the bets are level a timeout costs nothing: the player checks and stays in the hand.
    /// </summary>
    [Test]
    public async Task Test_Timeout_ShouldCheck_WhenThereIsNothingToCall()
    {
        await StartHandAsync(EXPIRING_TURN_TIMEOUT);
        await CallEveryoneIntoTheFlopAsync();

        var actor = _game.CurrentPlayer;
        Assert.That(_game.AmountToCall(actor), Is.Zero);

        await Wait.UntilAsync(() => actor.HasActed, "the player on turn to be checked automatically");
        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actor.HasFolded, Is.False);
            Assert.That(actor.HasActed, Is.True);
        }
    }

    [Test]
    public async Task Test_TurnTimer_ShouldStopOnceTheHandIsFinished()
    {
        await StartHandAsync(IDLE_TURN_TIMEOUT);
        await _game.CancelAsync();
        _recorder.Clear();

        await Wait.ForQuietPeriodAsync(IDLE_TURN_TIMEOUT * 4);

        Assert.That(_recorder.Entries, Is.Empty);
    }

    [Test]
    public async Task Test_TwoRacingCalls_ShouldOnlyCommitChipsOnce()
    {
        await StartHandAsync(PokerConstants.TURN_TIMEOUT);
        var actor = _game.CurrentPlayer;
        var stackBefore = actor.Stack;
        var toCall = _game.AmountToCall(actor);

        await Task.WhenAll(
            _game.CallAsync(actor.User),
            _game.CallAsync(actor.User));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actor.Stack, Is.EqualTo(stackBefore - toCall));
            Assert.That(_game.CurrentPlayer, Is.Not.SameAs(actor));
        }
    }

    [Test]
    public async Task Test_RacingCallAndFold_ShouldOnlyApplyTheFirstToTakeTheLock()
    {
        await StartHandAsync(PokerConstants.TURN_TIMEOUT);
        var actor = _game.CurrentPlayer;
        var stackBefore = actor.Stack;
        var toCall = _game.AmountToCall(actor);

        await Task.WhenAll(
            _game.CallAsync(actor.User),
            _game.FoldAsync(actor.User));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actor.HasFolded, Is.False);
            Assert.That(actor.Stack, Is.EqualTo(stackBefore - toCall));
        }
    }

    private async Task<IReadOnlyList<IUser>> StartHandAsync(TimeSpan turnTimeout)
    {
        _game = new PokerGame(_randomService, _recorder.TemplatesManager, _configuration, _moneyService,
            turnTimeout);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("poker-hand", _game.GameId);
        _recorder.MaskGameId("poker", _game.GameId);

        var users = GameUsers.Players(3);
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        return users;
    }

    private async Task CallEveryoneIntoTheFlopAsync()
    {
        while (_game.Phase == PokerPhase.Preflop)
        {
            var player = _game.CurrentPlayer;
            if (_game.AmountToCall(player) == 0)
            {
                await _game.CheckAsync(player.User);
            }
            else
            {
                await _game.CallAsync(player.User);
            }
        }

        Assert.That(_game.Phase, Is.EqualTo(PokerPhase.Flop));
    }
}
