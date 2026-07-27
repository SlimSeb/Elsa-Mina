using ElsaMina.Commands.Economy;
using ElsaMina.Commands.Games.Poker;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Poker;

/// <summary>
/// Pins the lobby half of poker, which differs from the other card games in two ways worth freezing:
/// joining moves real bucks, and only a seated player may start the hand.
/// </summary>
[TestFixture]
public class PokerLobbyFlowTest
{
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

        _game = new PokerGame(_randomService, _recorder.TemplatesManager, _configuration, _moneyService);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("poker-hand", _game.GameId);
        _recorder.MaskGameId("poker", _game.GameId);
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    [Test]
    public async Task Test_Join_ShouldTakeTheBuyInAndSeatThePlayer()
    {
        var (success, messageKey, args) = await _game.JoinAsync(GameUsers.User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(messageKey, Is.EqualTo("poker_join_success"));
            Assert.That(args, Is.EqualTo(new object[] { "player1", PokerConstants.DEFAULT_BUY_IN }));
            Assert.That(_game.Players[0].Stack, Is.EqualTo(PokerConstants.DEFAULT_BUY_IN));
            Assert.That(_recorder.PanelTrace(), Is.EqualTo(new[] { "poker-#-0 new" }));
        }

        await _moneyService.Received(1).AddAsync("testroom", "player1", -PokerConstants.DEFAULT_BUY_IN);
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenTheBalanceIsBelowTheBuyIn()
    {
        _moneyService.GetBalanceAsync("testroom", "pauper").Returns(Task.FromResult(5L));

        var (success, messageKey, args) = await _game.JoinAsync(GameUsers.User("pauper"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("poker_join_insufficient_funds"));
            Assert.That(args, Is.EqualTo(new object[] { PokerConstants.DEFAULT_BUY_IN, 5L }));
            Assert.That(_game.Players, Is.Empty);
        }

        await _moneyService.DidNotReceive().AddAsync("testroom", "pauper", Arg.Any<long>());
    }

    /// <summary>
    /// In "for fun" mode the buy-in only seeds the chip stack, so no balance is read and no bucks move.
    /// </summary>
    [Test]
    public async Task Test_Join_ShouldNotTouchBucks_WhenTheGameIsForFun()
    {
        _game.IsForFun = true;

        var (success, _, _) = await _game.JoinAsync(GameUsers.User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players[0].Stack, Is.EqualTo(PokerConstants.DEFAULT_BUY_IN));
        }

        await _moneyService.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>());
        await _moneyService.DidNotReceive().GetBalanceAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenAlreadyJoined()
    {
        var user = GameUsers.User("player1");
        await _game.JoinAsync(user);

        var (success, messageKey, _) = await _game.JoinAsync(user);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("poker_join_already_joined"));
        }
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenTableIsFull()
    {
        foreach (var user in GameUsers.Players(PokerConstants.MAX_PLAYERS))
        {
            await _game.JoinAsync(user);
        }

        var (success, messageKey, _) = await _game.JoinAsync(GameUsers.User("latecomer"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("poker_join_full"));
            Assert.That(_game.Players, Has.Count.EqualTo(PokerConstants.MAX_PLAYERS));
        }
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenTheHandHasStarted()
    {
        await StartHandAsync();

        var (success, messageKey, _) = await _game.JoinAsync(GameUsers.User("latecomer"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("poker_join_already_started"));
        }
    }

    [Test]
    public async Task Test_Start_ShouldBeRejected_WhenBelowTheMinimumPlayerCount()
    {
        var user = GameUsers.User("player1");
        await _game.JoinAsync(user);

        await _game.StartAsync(user);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PokerPhase.Lobby));
            Assert.That(_recorder.EntriesOfKind("reply"),
                Is.EqualTo(new[] { "reply poker_start_not_enough_players" }));
        }
    }

    /// <summary>
    /// Poker is the one seated card game that refuses to be started by someone who is not playing.
    /// </summary>
    [Test]
    public async Task Test_Start_ShouldBeRejected_WhenTriggeredByANonPlayer()
    {
        foreach (var user in GameUsers.Players(PokerConstants.MIN_PLAYERS))
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(GameUsers.User("spectator"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PokerPhase.Lobby));
            Assert.That(_recorder.EntriesOfKind("reply"), Is.EqualTo(new[] { "reply poker_start_not_a_player" }));
        }
    }

    [Test]
    public async Task Test_Start_ShouldBeRejected_WhenTheHandHasAlreadyStarted()
    {
        var users = await StartHandAsync();
        _recorder.Clear();

        await _game.StartAsync(users[0]);

        Assert.That(_recorder.EntriesOfKind("reply"), Is.EqualTo(new[] { "reply poker_start_already_started" }));
    }

    /// <summary>
    /// Cancelling mid-hand hands every player back what they still own: the chips in front of them plus
    /// everything they have already pushed into the pot.
    /// </summary>
    [Test]
    public async Task Test_Cancel_ShouldRefundStacksAndCommittedChips()
    {
        var users = await StartHandAsync();
        _recorder.Clear();

        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PokerPhase.Finished));
            Assert.That(_recorder.PanelTrace().First(), Is.EqualTo("poker-#-0 clear"));
            foreach (var player in _game.Players)
            {
                await _moneyService.Received(1)
                    .AddAsync("testroom", player.UserId, player.Stack + player.Committed);
            }
        }

        Assert.That(_game.Players.Sum(player => player.Stack + player.Committed),
            Is.EqualTo(users.Count * PokerConstants.DEFAULT_BUY_IN));
    }

    [Test]
    public async Task Test_Cancel_ShouldBeIdempotent()
    {
        await StartHandAsync();
        await _game.CancelAsync();
        _recorder.Clear();

        await _game.CancelAsync();

        Assert.That(_recorder.Entries, Is.Empty);
    }

    private async Task<IReadOnlyList<IUser>> StartHandAsync()
    {
        var users = GameUsers.Players(3);
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        return users;
    }
}
