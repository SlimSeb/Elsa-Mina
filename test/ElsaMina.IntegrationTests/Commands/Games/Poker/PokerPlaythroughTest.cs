using ElsaMina.Commands.Economy;
using ElsaMina.Commands.Games.Poker;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Poker;

/// <summary>
/// Drives a complete poker hand from the lobby to the showdown against a deterministic deal, and pins
/// the whole interaction trace. Poker posts its panels differently from the other card games (private
/// updatable panels rather than HTML pages, and a segment counter instead of a single panel id), which
/// is exactly the part that has to survive being moved into a shared base.
/// </summary>
[TestFixture]
public class PokerPlaythroughTest
{
    private const int PLAYER_COUNT = 3;

    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private IMoneyService _moneyService;
    private PokerGame _game;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>(); // ShuffleInPlace is a no-op -> deterministic deal
        _configuration = Substitute.For<IConfiguration>();
        _moneyService = Substitute.For<IMoneyService>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");
        _moneyService.GetBalanceAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(1000L));

        _game = new PokerGame(_randomService, _recorder.TemplatesManager, _configuration, _moneyService);
        _game.Context = _recorder.Context;
        // The hand panels embed the game id too, so mask the longer identifier first.
        _recorder.MaskGameId("poker-hand", _game.GameId);
        _recorder.MaskGameId("poker", _game.GameId);
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    [Test]
    public async Task Test_Hand_ShouldProduceTheExpectedInteractionTrace()
    {
        await PlayHandAsync();

        Assert.That(_game.Phase, Is.EqualTo(PokerPhase.Finished));
        TraceGolden.Verify("poker-hand", _recorder.CompressedTrace());
    }

    [Test]
    public async Task Test_Hand_ShouldReachShowdownWithAFullBoard()
    {
        await PlayHandAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.WentToShowdown, Is.True);
            Assert.That(_game.CommunityCards, Has.Count.EqualTo(5));
            Assert.That(_game.Players.All(player => player.HoleCards.Count == 2), Is.True);
            Assert.That(_game.Pots, Is.Not.Empty);
        }
    }

    /// <summary>
    /// Nobody folds and nobody raises, so the pot is exactly the big blind from every player and the
    /// chips are conserved: what the players hold at the end equals what they bought in with.
    /// </summary>
    [Test]
    public async Task Test_Hand_ShouldConserveChipsAcrossTheHand()
    {
        await PlayHandAsync();

        var expectedTotal = PLAYER_COUNT * PokerConstants.DEFAULT_BUY_IN;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Players.Sum(player => player.Stack), Is.EqualTo(expectedTotal));
            Assert.That(_game.Pots.Sum(pot => pot.Amount),
                Is.EqualTo(PLAYER_COUNT * _game.BigBlindAmount));
            Assert.That(_game.Players.Sum(player => player.Winnings),
                Is.EqualTo(PLAYER_COUNT * _game.BigBlindAmount));
        }
    }

    [Test]
    public async Task Test_Hand_ShouldTakeTheBuyInFromEveryPlayerAndPayTheStacksBack()
    {
        await PlayHandAsync();

        using (Assert.EnterMultipleScope())
        {
            foreach (var player in _game.Players)
            {
                await _moneyService.Received(1)
                    .AddAsync("testroom", player.UserId, -PokerConstants.DEFAULT_BUY_IN);
                await _moneyService.Received(1).AddAsync("testroom", player.UserId, player.Stack);
            }
        }
    }

    private async Task PlayHandAsync()
    {
        var users = GameUsers.Players(PLAYER_COUNT);
        await _game.BeginJoinPhaseAsync();
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        await PlayOutAsync();
    }

    /// <summary>
    /// Everyone checks when they can and calls otherwise, which walks the hand through every street to
    /// the showdown without any branching on the cards dealt.
    /// </summary>
    private async Task PlayOutAsync()
    {
        var safetyLimit = 100;
        while (_game.CurrentPlayer is not null && _game.Phase is not (PokerPhase.Showdown or PokerPhase.Finished)
                                               && safetyLimit-- > 0)
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

        Assert.That(safetyLimit, Is.GreaterThan(0), "the hand did not terminate");
    }
}
