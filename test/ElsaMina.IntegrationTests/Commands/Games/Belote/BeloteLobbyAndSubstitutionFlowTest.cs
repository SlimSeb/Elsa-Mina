using ElsaMina.Commands.Games.Belote;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Belote;

/// <summary>
/// Pins the lobby and substitution flows of belote. Belote seats exactly four players and has no
/// "leave" of its own, so the shapes it shares with the other card games have to survive unchanged
/// while the differences stay differences.
/// </summary>
[TestFixture]
public class BeloteLobbyAndSubstitutionFlowTest
{
    private GameInteractionRecorder _recorder;
    private IRandomService _randomService;
    private IConfiguration _configuration;
    private IBeloteStatsService _statsService;
    private BeloteGame _game;
    private IReadOnlyList<IUser> _users;

    [SetUp]
    public void SetUp()
    {
        _recorder = new GameInteractionRecorder();
        _randomService = Substitute.For<IRandomService>();
        _configuration = Substitute.For<IConfiguration>();
        _statsService = Substitute.For<IBeloteStatsService>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");

        _game = new BeloteGame(_randomService, _recorder.TemplatesManager, _configuration, _statsService);
        _game.Context = _recorder.Context;
        _recorder.MaskGameId("belote", _game.GameId);
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
            Assert.That(messageKey, Is.EqualTo("belote_join_success"));
            Assert.That(args, Is.EqualTo(new object[] { "player1" }));
            Assert.That(_recorder.PanelTrace(), Is.EqualTo(new[] { "belote-# new" }));
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
            Assert.That(messageKey, Is.EqualTo("belote_join_already_joined"));
        }
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenTableIsFull()
    {
        foreach (var user in GameUsers.Players(BeloteConstants.PLAYER_COUNT))
        {
            await _game.JoinAsync(user);
        }

        var (success, messageKey, _) = await _game.JoinAsync(GameUsers.User("latecomer"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("belote_join_full"));
        }
    }

    [Test]
    public async Task Test_Join_ShouldBeRejected_WhenTheDealHasStarted()
    {
        await StartDealAsync();

        var (success, messageKey, _) = await _game.JoinAsync(GameUsers.User("latecomer"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("belote_join_already_started"));
        }
    }

    [Test]
    public async Task Test_Start_ShouldBeRejected_WhenTheTableIsNotExactlyFull()
    {
        foreach (var user in GameUsers.Players(BeloteConstants.PLAYER_COUNT - 1))
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(GameUsers.User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(BelotePhase.Lobby));
            Assert.That(_recorder.EntriesOfKind("reply"),
                Is.EqualTo(new[] { "reply belote_start_not_enough_players" }));
        }
    }

    [Test]
    public async Task Test_Start_ShouldBeRejected_WhenTheDealHasAlreadyStarted()
    {
        await StartDealAsync();
        _recorder.Clear();

        await _game.StartAsync(_users[0]);

        Assert.That(_recorder.EntriesOfKind("reply"),
            Is.EqualTo(new[] { "reply belote_start_already_started" }));
    }

    /// <summary>
    /// The lobby panel is wiped and the table posted afresh at the bottom of the chat, the same way
    /// every resolved trick re-posts it, rather than being updated in place high up in the scrollback.
    /// </summary>
    [Test]
    public async Task Test_Start_ShouldRepostThePublicPanel()
    {
        foreach (var user in GameUsers.Players(BeloteConstants.PLAYER_COUNT))
        {
            await _game.JoinAsync(user);
        }

        _recorder.Clear();
        await _game.StartAsync(GameUsers.User("player1"));

        Assert.That(_recorder.PanelTrace().Take(2), Is.EqualTo(new[] { "belote-# clear", "belote-# new" }));
    }

    [Test]
    public async Task Test_Start_ShouldBeRejected_WhenTriggeredByANonPlayer()
    {
        foreach (var user in GameUsers.Players(BeloteConstants.PLAYER_COUNT))
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(GameUsers.User("spectator"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(BelotePhase.Lobby));
            Assert.That(_recorder.EntriesOfKind("reply"),
                Is.EqualTo(new[] { "reply belote_start_not_a_player" }));
        }
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
            Assert.That(_game.Phase, Is.EqualTo(BelotePhase.Finished));
            Assert.That(_recorder.Entries, Is.EqualTo(new[]
            {
                "tpl Games/Belote/BeloteCancelled",
                "panel belote-# update"
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
            Assert.That(messageKey, Is.EqualTo("belote_sub_not_active"));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldFail_WhenNotAPlayer()
    {
        await StartDealAsync();

        var (success, messageKey, _) = await _game.RequestSubAsync(GameUsers.User("stranger"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("belote_sub_not_a_player"));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldRepostTheSubPanel()
    {
        await StartDealAsync();
        _recorder.Clear();

        var (success, _, _) = await _game.RequestSubAsync(_users[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_recorder.Entries, Is.EqualTo(new[]
            {
                "reply belote_sub_requested",
                "tpl Games/Belote/BeloteSub",
                "panel belote-#-sub new"
            }));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldCancelThePendingRequest_WhenRepeated()
    {
        await StartDealAsync();
        await _game.RequestSubAsync(_users[1]);
        _recorder.Clear();

        await _game.RequestSubAsync(_users[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Players[1].WantsSub, Is.False);
            Assert.That(_recorder.Entries, Is.EqualTo(new[]
            {
                "reply belote_sub_cancelled",
                "panel belote-#-sub clear"
            }));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldKeepTheSeatsHandTeamAndCapturedPile()
    {
        await StartDealAsync();
        await _game.BidAsync(_game.CurrentPlayer.User, pass: false, null);
        await _game.PlayAsync(_game.CurrentPlayer.User, _game.GetLegalMoves(_game.CurrentPlayer).First());

        var seat = _game.Players[1];
        var handBefore = seat.Hand.ToList();
        var teamBefore = seat.Team;
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
            Assert.That(_game.Players[1].Team, Is.EqualTo(teamBefore));
            // The turn belongs to the seat, so it stays put across the handover.
            Assert.That(_game.CurrentPlayer, Is.SameAs(currentSeatBefore));
            Assert.That(_game.CurrentPlayer, Is.SameAs(seat));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldCloseTheLeavingPlayersPage()
    {
        await StartDealAsync();
        await _game.RequestSubAsync(_users[1]);
        _recorder.Clear();

        await _game.AcceptSubAsync(GameUsers.User("substitute"), "player2");

        Assert.That(_recorder.EntriesOfKind("close"), Is.EqualTo(new[] { "close player2 belote-#" }));
    }

    [Test]
    public async Task Test_AcceptSub_ShouldFail_WhenNothingIsPendingOrTheTargetIsWrong()
    {
        await StartDealAsync();

        var (nonePendingSuccess, nonePendingKey, _) =
            await _game.AcceptSubAsync(GameUsers.User("substitute"), null);
        await _game.RequestSubAsync(_users[1]);
        var (wrongTargetSuccess, wrongTargetKey, _) =
            await _game.AcceptSubAsync(GameUsers.User("substitute"), "player3");
        var (alreadySeatedSuccess, alreadySeatedKey, _) = await _game.AcceptSubAsync(_users[0], "player2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nonePendingSuccess, Is.False);
            Assert.That(nonePendingKey, Is.EqualTo("belote_sub_none_pending"));
            Assert.That(wrongTargetSuccess, Is.False);
            Assert.That(wrongTargetKey, Is.EqualTo("belote_sub_invalid_target"));
            Assert.That(alreadySeatedSuccess, Is.False);
            Assert.That(alreadySeatedKey, Is.EqualTo("belote_sub_already_player"));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldFail_WhenTheDealIsOver()
    {
        await StartDealAsync();
        await _game.RequestSubAsync(_users[1]);
        await _game.CancelAsync();

        var (success, messageKey, _) = await _game.AcceptSubAsync(GameUsers.User("substitute"), "player2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("belote_sub_not_active"));
        }
    }

    private async Task StartDealAsync()
    {
        _users = GameUsers.Players(BeloteConstants.PLAYER_COUNT);
        foreach (var user in _users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(_users[0]);
    }
}
