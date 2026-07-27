using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.IntegrationTests.Fixtures;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Games.Tarot;

/// <summary>
/// Pins the substitution flow: requesting, cancelling, staff forcing a request, accepting one, and the
/// panel writes each step produces. A substituted seat must keep everything it owned, which is the
/// property most at risk when the flow moves into a shared base class.
/// </summary>
[TestFixture]
public class TarotSubstitutionFlowTest
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
    public async Task Test_RequestSub_ShouldFail_WhileStillInTheLobby()
    {
        await _game.JoinAsync(GameUsers.User("player1"));

        var (success, messageKey, _) = await _game.RequestSubAsync(GameUsers.User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_not_active"));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldFail_WhenTheDealIsOver()
    {
        await StartDealAsync();
        await _game.CancelAsync();

        var (success, messageKey, _) = await _game.RequestSubAsync(_users[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_not_active"));
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
            Assert.That(messageKey, Is.EqualTo("tarot_sub_not_a_player"));
        }
    }

    /// <summary>
    /// A fresh request re-posts the sub panel at the bottom of the chat, which means wiping the old one
    /// first and then sending a brand new panel rather than updating in place.
    /// </summary>
    [Test]
    public async Task Test_RequestSub_ShouldRepostTheSubPanel()
    {
        await StartDealAsync();
        _recorder.Clear();

        var (success, _, _) = await _game.RequestSubAsync(_users[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players[1].WantsSub, Is.True);
            Assert.That(_recorder.Entries, Is.EqualTo(new[]
            {
                "reply tarot_sub_requested",
                "tpl Games/Tarot/TarotSub",
                "panel tarot-#-sub new"
            }));
        }
    }

    [Test]
    public async Task Test_RequestSub_ShouldCancelThePendingRequest_WhenRepeated()
    {
        await StartDealAsync();
        await _game.RequestSubAsync(_users[1]);
        _recorder.Clear();

        var (success, _, _) = await _game.RequestSubAsync(_users[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players[1].WantsSub, Is.False);
            Assert.That(_recorder.Entries, Is.EqualTo(new[]
            {
                "reply tarot_sub_cancelled",
                "panel tarot-#-sub clear"
            }));
        }
    }

    [Test]
    public async Task Test_ForceRequestSub_ShouldMarkTheTargetSeat()
    {
        await StartDealAsync();
        _recorder.Clear();

        var (success, _, _) = await _game.ForceRequestSubAsync("player2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players[1].WantsSub, Is.True);
            Assert.That(_recorder.Entries, Is.EqualTo(new[]
            {
                "reply tarot_sub_force_requested",
                "tpl Games/Tarot/TarotSub",
                "panel tarot-#-sub new"
            }));
        }
    }

    [Test]
    public async Task Test_ForceRequestSub_ShouldFail_WhenTheSeatIsAlreadyLookingForASub()
    {
        await StartDealAsync();
        await _game.ForceRequestSubAsync("player2");

        var (success, messageKey, args) = await _game.ForceRequestSubAsync("player2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_force_already"));
            Assert.That(args, Is.EqualTo(new object[] { "player2" }));
        }
    }

    [Test]
    public async Task Test_ForceRequestSub_ShouldFail_WhenTheTargetIsMissingOrUnknown()
    {
        await StartDealAsync();

        var (emptySuccess, emptyKey, _) = await _game.ForceRequestSubAsync("  ");
        var (unknownSuccess, unknownKey, _) = await _game.ForceRequestSubAsync("stranger");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(emptySuccess, Is.False);
            Assert.That(emptyKey, Is.EqualTo("tarot_sub_force_no_target"));
            Assert.That(unknownSuccess, Is.False);
            Assert.That(unknownKey, Is.EqualTo("tarot_sub_force_not_a_player"));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldKeepTheSeatsHandCapturedPileAndTurnPosition()
    {
        await StartDealAsync();
        await _game.BidAsync(_game.CurrentPlayer.User, TarotBid.GardeSans);
        await _game.BidAsync(_game.CurrentPlayer.User, TarotBid.Pass);

        var seat = _game.Players[1];
        var handBefore = seat.Hand.ToList();
        var capturedBefore = seat.CapturedPile.ToList();
        var currentPlayerBefore = _game.CurrentPlayer.UserId;
        var seatHadBid = seat.HasBid;
        await _game.RequestSubAsync(_users[1]);

        var (success, _, _) = await _game.AcceptSubAsync(GameUsers.User("substitute"), "player2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players[1], Is.SameAs(seat));
            Assert.That(_game.Players[1].UserId, Is.EqualTo("substitute"));
            Assert.That(_game.Players[1].WantsSub, Is.False);
            Assert.That(_game.Players[1].Hand, Is.EqualTo(handBefore));
            Assert.That(_game.Players[1].CapturedPile, Is.EqualTo(capturedBefore));
            Assert.That(_game.Players[1].HasBid, Is.EqualTo(seatHadBid));
            Assert.That(_game.CurrentPlayer.UserId, Is.EqualTo(currentPlayerBefore));
            Assert.That(_game.Players.Any(player => player.UserId == "player2"), Is.False);
        }
    }

    /// <summary>
    /// Accepting closes the leaving player's private page before the seat changes hands, so the old
    /// occupant cannot keep reading the new hand.
    /// </summary>
    [Test]
    public async Task Test_AcceptSub_ShouldCloseTheLeavingPlayersPage()
    {
        await StartDealAsync();
        await _game.RequestSubAsync(_users[1]);
        _recorder.Clear();

        await _game.AcceptSubAsync(GameUsers.User("substitute"), "player2");

        var entries = _recorder.Entries.ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_recorder.EntriesOfKind("close"), Is.EqualTo(new[] { "close player2 tarot-#" }));
            Assert.That(entries.IndexOf("close player2 tarot-#"),
                Is.LessThan(entries.IndexOf("page substitute tarot-#")));
            Assert.That(_recorder.PanelTrace().Last(), Is.EqualTo("tarot-#-sub clear"));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldTakeTheOnlyPendingSeat_WhenNoTargetIsGiven()
    {
        await StartDealAsync();
        await _game.RequestSubAsync(_users[2]);

        var (success, _, _) = await _game.AcceptSubAsync(GameUsers.User("substitute"), null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(_game.Players[2].UserId, Is.EqualTo("substitute"));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldFail_WhenNoRequestIsPendingForThatSeat()
    {
        await StartDealAsync();
        await _game.RequestSubAsync(_users[1]);

        var (success, messageKey, _) = await _game.AcceptSubAsync(GameUsers.User("substitute"), "player3");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_invalid_target"));
            Assert.That(_game.Players[2].UserId, Is.EqualTo("player3"));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldFail_WhenNothingIsPending()
    {
        await StartDealAsync();

        var (success, messageKey, _) = await _game.AcceptSubAsync(GameUsers.User("substitute"), null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_none_pending"));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldFail_WhenTheAccepterIsAlreadySeated()
    {
        await StartDealAsync();
        await _game.RequestSubAsync(_users[1]);

        var (success, messageKey, _) = await _game.AcceptSubAsync(_users[0], "player2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_already_player"));
        }
    }

    [Test]
    public async Task Test_AcceptSub_ShouldFail_WhileStillInTheLobby()
    {
        await _game.JoinAsync(GameUsers.User("player1"));

        var (success, messageKey, _) = await _game.AcceptSubAsync(GameUsers.User("substitute"), "player1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("tarot_sub_not_active"));
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
            Assert.That(messageKey, Is.EqualTo("tarot_sub_not_active"));
        }
    }

    /// <summary>
    /// The sub panel only lives while at least one seat is looking for a replacement: it is wiped as
    /// soon as the last pending request is resolved.
    /// </summary>
    [Test]
    public async Task Test_SubPanel_ShouldBeWipedOnceTheLastRequestIsResolved()
    {
        await StartDealAsync();
        await _game.RequestSubAsync(_users[1]);
        await _game.RequestSubAsync(_users[2]);
        _recorder.Clear();

        await _game.AcceptSubAsync(GameUsers.User("substitute1"), "player2");
        var panelAfterFirst = _recorder.PanelTrace().Last();
        _recorder.Clear();
        await _game.AcceptSubAsync(GameUsers.User("substitute2"), "player3");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(panelAfterFirst, Is.EqualTo("tarot-#-sub update"));
            Assert.That(_recorder.PanelTrace().Last(), Is.EqualTo("tarot-#-sub clear"));
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
}
