using ElsaMina.Commands.Games.President;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.President;

/// <summary>
/// Pins the lobby and substitution flows of président, including the seat state that has to survive a
/// handover: hand, role, cumulative score and turn position.
/// </summary>
[TestFixture]
public class PresidentLobbyAndSubstitutionFlowTest
{
    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private PresidentGame _game;
    private IReadOnlyList<IUser> _users;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>();
        _configuration = Substitute.For<IConfiguration>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");

        _game = new PresidentGame(_randomService, _recorder.TemplatesManager, _configuration);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("president", _game.GameId);
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    [Test]
    public async Task Test_Join_ShouldSeatThePlayerAndRefreshTheLobbyPanel()
    {
        var (success, messageKey, args) = await _game.JoinAsync(GameUsers.User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(messageKey, Is.EqualTo("president_join_success"));
            Assert.That(args, Is.EqualTo(new object[] { "player1" }));
            Assert.That(_recorder.PanelTrace(), Is.EqualTo(new[] { "president-# new" }));
        }
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
            Assert.That(messageKey, Is.EqualTo("president_join_already_joined"));
        }
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenTableIsFull()
    {
        foreach (var user in GameUsers.Players(PresidentConstants.MAX_PLAYERS))
        {
            await _game.JoinAsync(user);
        }

        var (success, messageKey, _) = await _game.JoinAsync(GameUsers.User("latecomer"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("president_join_full"));
            Assert.That(_game.Players, Has.Count.EqualTo(PresidentConstants.MAX_PLAYERS));
        }
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenTheGameHasStarted()
    {
        await StartGameAsync();

        var (success, messageKey, _) = await _game.JoinAsync(GameUsers.User("latecomer"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("president_join_already_started"));
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
            Assert.That(messageKey, Is.EqualTo("president_quit_success"));
            Assert.That(args, Is.EqualTo(new object[] { "player1" }));
            Assert.That(_game.Players.Select(player => player.UserId), Is.EqualTo(new[] { "player2" }));
            Assert.That(_recorder.PanelTrace(), Is.EqualTo(new[] { "president-# update" }));
        }
    }

    [Test]
    public async Task Test_Leave_ShouldBeRejected_WhenNotJoinedOrAlreadyStarted()
    {
        await _game.JoinAsync(GameUsers.User("player1"));
        var (strangerSuccess, strangerKey, _) = await _game.LeaveAsync(GameUsers.User("stranger"));

        await StartGameAsync();
        var (startedSuccess, startedKey, _) = await _game.LeaveAsync(_users[0]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strangerSuccess, Is.False);
            Assert.That(strangerKey, Is.EqualTo("president_quit_not_joined"));
            Assert.That(startedSuccess, Is.False);
            Assert.That(startedKey, Is.EqualTo("president_quit_already_started"));
        }
    }

    [Test]
    public async Task Test_Start_ShouldBeRejected_WhenBelowTheMinimumPlayerCount()
    {
        foreach (var user in GameUsers.Players(PresidentConstants.MIN_PLAYERS - 1))
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(GameUsers.User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Lobby));
            Assert.That(_recorder.EntriesOfKind("reply"),
                Is.EqualTo(new[] { "reply president_start_not_enough_players" }));
        }
    }

    [Test]
    public async Task Test_Start_ShouldBeRejected_WhenTheGameHasAlreadyStarted()
    {
        await StartGameAsync();
        _recorder.Clear();

        await _game.StartAsync(_users[0]);

        Assert.That(_recorder.EntriesOfKind("reply"),
            Is.EqualTo(new[] { "reply president_start_already_started" }));
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
            Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Finished));
            Assert.That(_recorder.Entries, Is.EqualTo(new[]
            {
                "tpl Games/President/PresidentCancelled",
                "panel president-# update"
            }));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldFail_WhileStillInTheLobby()
    {
        await _game.JoinAsync(GameUsers.User("player1"));

        var (success, messageKey, _) = await _game.RequestSubAsync(GameUsers.User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("president_sub_not_active"));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldFail_WhenNotAPlayer()
    {
        await StartGameAsync();

        var (success, messageKey, _) = await _game.RequestSubAsync(GameUsers.User("stranger"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("president_sub_not_a_player"));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldRepostTheSubPanel()
    {
        await StartGameAsync();
        _recorder.Clear();

        var (success, _, _) = await _game.RequestSubAsync(_users[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_recorder.Entries, Is.EqualTo(new[]
            {
                "reply president_sub_requested",
                "tpl Games/President/PresidentSub",
                "panel president-#-sub new"
            }));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldCancelThePendingRequest_WhenRepeated()
    {
        await StartGameAsync();
        await _game.RequestSubAsync(_users[1]);
        _recorder.Clear();

        await _game.RequestSubAsync(_users[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Players[1].WantsSub, Is.False);
            Assert.That(_recorder.Entries, Is.EqualTo(new[]
            {
                "reply president_sub_cancelled",
                "panel president-#-sub clear"
            }));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldKeepTheSeatsHandScoreAndTurnPosition()
    {
        await StartGameAsync();
        var leader = _game.CurrentPlayer;
        var (rank, count) = _game.GetLegalPlays(leader)[0];
        await _game.PlayAsync(leader.User, rank, count);

        var seat = _game.Players[1];
        var handBefore = seat.Hand.ToList();
        var scoreBefore = seat.Score;
        var currentSeatBefore = _game.CurrentPlayer;
        await _game.RequestSubAsync(_users[1]);

        var (success, _, _) = await _game.AcceptSubAsync(GameUsers.User("substitute"), "player2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players[1], Is.SameAs(seat));
            Assert.That(_game.Players[1].UserId, Is.EqualTo("substitute"));
            Assert.That(_game.Players[1].WantsSub, Is.False);
            Assert.That(_game.Players[1].Hand, Is.EqualTo(handBefore));
            Assert.That(_game.Players[1].Score, Is.EqualTo(scoreBefore));
            Assert.That(_game.CurrentPlayer, Is.SameAs(currentSeatBefore));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldCloseTheLeavingPlayersPage()
    {
        await StartGameAsync();
        await _game.RequestSubAsync(_users[1]);
        _recorder.Clear();

        await _game.AcceptSubAsync(GameUsers.User("substitute"), "player2");

        Assert.That(_recorder.EntriesOfKind("close"), Is.EqualTo(new[] { "close player2 president-#" }));
    }

    [Test]
    public async Task Test_AcceptSub_ShouldFail_WhenNothingIsPendingOrTheTargetIsWrong()
    {
        await StartGameAsync();

        var (nonePendingSuccess, nonePendingKey, _) =
            await _game.AcceptSubAsync(GameUsers.User("substitute"), null);
        await _game.RequestSubAsync(_users[1]);
        var (wrongTargetSuccess, wrongTargetKey, _) =
            await _game.AcceptSubAsync(GameUsers.User("substitute"), "player3");
        var (alreadySeatedSuccess, alreadySeatedKey, _) = await _game.AcceptSubAsync(_users[0], "player2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nonePendingSuccess, Is.False);
            Assert.That(nonePendingKey, Is.EqualTo("president_sub_none_pending"));
            Assert.That(wrongTargetSuccess, Is.False);
            Assert.That(wrongTargetKey, Is.EqualTo("president_sub_invalid_target"));
            Assert.That(alreadySeatedSuccess, Is.False);
            Assert.That(alreadySeatedKey, Is.EqualTo("president_sub_already_player"));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldFail_WhenTheGameIsOver()
    {
        await StartGameAsync();
        await _game.RequestSubAsync(_users[1]);
        await _game.CancelAsync();

        var (success, messageKey, _) = await _game.AcceptSubAsync(GameUsers.User("substitute"), "player2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("president_sub_not_active"));
        }
    }

    private async Task StartGameAsync()
    {
        _users = GameUsers.Players(4);
        foreach (var user in _users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(_users[0]);
    }
}
