using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Tarot;

/// <summary>
/// Pins the lobby half of the tarot game: joining, leaving and starting, together with the resource
/// keys each rejection produces and the panel writes each accepted action triggers.
/// </summary>
[TestFixture]
public class TarotLobbyFlowTest
{
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

        _game = new TarotGame(_randomService, _recorder.TemplatesManager, _configuration, _statsService);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("tarot", _game.GameId);
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    [Test]
    public async Task Test_Join_ShouldSeatThePlayerAndRefreshTheLobbyPanel()
    {
        var user = GameUsers.User("player1");

        var (success, messageKey, args) = await _game.JoinAsync(user);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(messageKey, Is.EqualTo("tarot_join_success"));
            Assert.That(args, Is.EqualTo(new object[] { "player1" }));
            Assert.That(_game.Players.Select(player => player.UserId), Is.EqualTo(new[] { "player1" }));
            Assert.That(_recorder.PanelTrace(), Is.EqualTo(new[] { "tarot-# new" }));
        }
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenAlreadyJoined()
    {
        var user = GameUsers.User("player1");
        await _game.JoinAsync(user);
        _recorder.Clear();

        var (success, messageKey, _) = await _game.JoinAsync(user);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_join_already_joined"));
            Assert.That(_game.Players, Has.Count.EqualTo(1));
            Assert.That(_recorder.Entries, Is.Empty);
        }
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenTableIsFull()
    {
        foreach (var user in GameUsers.Players(TarotConstants.MAX_PLAYERS))
        {
            await _game.JoinAsync(user);
        }

        var (success, messageKey, _) = await _game.JoinAsync(GameUsers.User("latecomer"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_join_full"));
            Assert.That(_game.Players, Has.Count.EqualTo(TarotConstants.MAX_PLAYERS));
        }
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenTheDealHasStarted()
    {
        await StartWithAsync(TarotConstants.MIN_PLAYERS);

        var (success, messageKey, _) = await _game.JoinAsync(GameUsers.User("latecomer"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_join_already_started"));
        }
    }

    [Test]
    public async Task Test_Leave_ShouldFreeTheSeatAndRefreshTheLobbyPanel()
    {
        var users = GameUsers.Players(2);
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        _recorder.Clear();
        var (success, messageKey, args) = await _game.LeaveAsync(users[0]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(messageKey, Is.EqualTo("tarot_quit_success"));
            Assert.That(args, Is.EqualTo(new object[] { "player1" }));
            Assert.That(_game.Players.Select(player => player.UserId), Is.EqualTo(new[] { "player2" }));
            Assert.That(_recorder.PanelTrace(), Is.EqualTo(new[] { "tarot-# update" }));
        }
    }

    [Test]
    public async Task Test_Leave_ShouldBeRejected_WhenNotJoined()
    {
        await _game.JoinAsync(GameUsers.User("player1"));

        var (success, messageKey, _) = await _game.LeaveAsync(GameUsers.User("stranger"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_quit_not_joined"));
            Assert.That(_game.Players, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_Leave_ShouldBeRejected_WhenTheDealHasStarted()
    {
        var users = await StartWithAsync(TarotConstants.MIN_PLAYERS);

        var (success, messageKey, _) = await _game.LeaveAsync(users[0]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_quit_already_started"));
            Assert.That(_game.Players, Has.Count.EqualTo(TarotConstants.MIN_PLAYERS));
        }
    }

    [Test]
    public async Task Test_Start_ShouldBeRejected_WhenBelowTheMinimumPlayerCount()
    {
        foreach (var user in GameUsers.Players(TarotConstants.MIN_PLAYERS - 1))
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(GameUsers.User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Lobby));
            Assert.That(_recorder.EntriesOfKind("reply"),
                Is.EqualTo(new[] { "reply tarot_start_not_enough_players" }));
        }
    }

    [Test]
    public async Task Test_Start_ShouldBeRejected_WhenTheDealHasAlreadyStarted()
    {
        var users = await StartWithAsync(TarotConstants.MIN_PLAYERS);
        _recorder.Clear();

        await _game.StartAsync(users[0]);

        Assert.That(_recorder.EntriesOfKind("reply"),
            Is.EqualTo(new[] { "reply tarot_start_already_started" }));
    }

    /// <summary>
    /// Tarot deliberately lets anyone in the room start a seated game, unlike poker where only a
    /// player may. Pinned here so the guard is not accidentally tightened while refactoring.
    /// </summary>
    [Test]
    public async Task Test_Start_ShouldBeAccepted_WhenTriggeredByANonPlayer()
    {
        foreach (var user in GameUsers.Players(TarotConstants.MIN_PLAYERS))
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(GameUsers.User("spectator"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Bidding));
            Assert.That(_recorder.EntriesOfKind("reply"), Is.Empty);
        }
    }

    [Test]
    public async Task Test_Start_ShouldBeAccepted_WhenTriggeredByAPlayerOtherThanTheFirstToJoin()
    {
        var users = GameUsers.Players(TarotConstants.MIN_PLAYERS);
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[^1]);

        Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Bidding));
    }

    [Test]
    public async Task Test_Cancel_ShouldReplaceTheLobbyPanelWithACancelledNotice()
    {
        await _game.BeginJoinPhaseAsync();
        await _game.JoinAsync(GameUsers.User("player1"));
        _recorder.Clear();

        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(TarotPhase.Finished));
            Assert.That(_recorder.Entries, Is.EqualTo(new[]
            {
                "tpl Games/Tarot/TarotCancelled",
                "panel tarot-# update"
            }));
        }
    }

    private async Task<IReadOnlyList<IUser>> StartWithAsync(int count)
    {
        var users = GameUsers.Players(count);
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        return users;
    }
}
