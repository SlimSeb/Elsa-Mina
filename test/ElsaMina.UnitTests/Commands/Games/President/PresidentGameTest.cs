using System.Globalization;
using ElsaMina.Commands.Games.President;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.President;

[TestFixture]
public class PresidentGameTest
{
    private IRandomService _randomService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private PresidentGame _game;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>(); // ShuffleInPlace is a no-op -> deterministic deal
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("ElsaMina");
        _configuration.Trigger.Returns("-");
        _context.RoomId.Returns("testroom");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        // Echo the resource key so the game log (built from GetString) is assertable in tests.
        _context.GetString(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>()).Returns(callInfo => callInfo.ArgAt<string>(0));
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        _game = new PresidentGame(_randomService, _templatesManager, _configuration);
        _game.Context = _context;
    }

    [TearDown]
    public async Task TearDown() => await _game.CancelAsync();

    private static IUser User(string id)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return user;
    }

    private async Task<IReadOnlyList<IUser>> JoinAndStartAsync(int count)
    {
        var users = Enumerable.Range(1, count).Select(index => User($"player{index}")).ToList();
        foreach (var user in users)
        {
            await _game.JoinAsync(user);
        }

        await _game.StartAsync(users[0]);
        return users;
    }

    /// <summary>
    /// Plays the current round to its end: every player greedily plays their lowest legal
    /// combination and passes when they cannot follow.
    /// </summary>
    private async Task DriveRoundAsync()
    {
        var safety = 0;
        while (_game.Phase == PresidentPhase.Playing && safety++ < 1000)
        {
            var current = _game.CurrentPlayer;
            var plays = _game.GetLegalPlays(current);
            if (plays.Count > 0)
            {
                await _game.PlayAsync(current.User, plays[0].Rank, plays[0].Count);
            }
            else
            {
                await _game.PassAsync(current.User);
            }
        }

        Assert.That(safety, Is.LessThan(1000), "the round did not terminate");
    }

    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(8)]
    public async Task Test_Start_ShouldDealWholeDeckAndBeginPlaying(int players)
    {
        await JoinAndStartAsync(players);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Playing));
            Assert.That(_game.RoundNumber, Is.EqualTo(1));
            Assert.That(_game.Players.Sum(player => player.Hand.Count), Is.EqualTo(52));
            Assert.That(_game.CurrentPlayer, Is.EqualTo(_game.Players[0]));
            Assert.That(_game.CurrentTrick.IsEmpty, Is.True);
        }
    }

    [Test]
    public async Task Test_Start_ShouldFail_WhenNotEnoughPlayers()
    {
        await _game.JoinAsync(User("player1"));
        await _game.JoinAsync(User("player2"));

        await _game.StartAsync(User("player1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Lobby));
            _context.Received(1).ReplyLocalizedMessage("president_start_not_enough_players", Arg.Any<object[]>());
        }
    }

    [Test]
    public async Task Test_Join_ShouldFail_WhenAlreadyJoined()
    {
        var user = User("player1");
        await _game.JoinAsync(user);

        var (success, messageKey, _) = await _game.JoinAsync(user);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("president_join_already_joined"));
        }
    }

    [Test]
    public async Task Test_Join_ShouldFail_WhenGameIsFull()
    {
        for (var index = 1; index <= PresidentConstants.MAX_PLAYERS; index++)
        {
            await _game.JoinAsync(User($"player{index}"));
        }

        var (success, messageKey, _) = await _game.JoinAsync(User("latecomer"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("president_join_full"));
        }
    }

    [Test]
    public async Task Test_Join_ShouldFail_WhenGameHasStarted()
    {
        await JoinAndStartAsync(3);

        var (success, messageKey, _) = await _game.JoinAsync(User("latecomer"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("president_join_already_started"));
        }
    }

    [Test]
    public async Task Test_GetLegalPlays_ShouldOfferEveryCountOfEachRank_WhenLeading()
    {
        await JoinAndStartAsync(3);
        // Deterministic deal: player1 holds two 3s (3♥ and 3♣) among others.
        var leader = _game.CurrentPlayer;

        var plays = _game.GetLegalPlays(leader);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plays, Does.Contain((3, 1)));
            Assert.That(plays, Does.Contain((3, 2)));
            Assert.That(plays, Does.Not.Contain((3, 3)));
        }
    }

    [Test]
    public async Task Test_GetLegalPlays_ShouldOnlyOfferEqualOrHigherRanks_WhenFollowing()
    {
        await JoinAndStartAsync(3);
        await _game.PlayAsync(_game.CurrentPlayer.User, PresidentCard.QUEEN, 1);

        var plays = _game.GetLegalPlays(_game.CurrentPlayer);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plays, Does.Contain((PresidentCard.QUEEN, 1)));
            Assert.That(plays, Does.Contain((PresidentCard.KING, 1)));
            Assert.That(plays, Does.Contain((PresidentCard.TWO, 1)));
            Assert.That(plays, Does.Not.Contain((3, 1)));
        }
    }

    [Test]
    public async Task Test_Play_ShouldMoveToNextPlayer_WhenPlayIsLegal()
    {
        await JoinAndStartAsync(3);
        var leader = _game.CurrentPlayer;

        await _game.PlayAsync(leader.User, 3, 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.CurrentTrick.Plays, Has.Count.EqualTo(1));
            Assert.That(_game.CurrentTrick.RequiredCount, Is.EqualTo(1));
            Assert.That(_game.CurrentTrick.TopRank, Is.EqualTo(3));
            Assert.That(leader.Hand, Has.Count.EqualTo(17));
            Assert.That(_game.CurrentPlayer, Is.EqualTo(_game.Players[1]));
        }
    }

    [Test]
    public async Task Test_Play_ShouldBeRejected_WhenRankIsTooLow()
    {
        await JoinAndStartAsync(3);
        await _game.PlayAsync(_game.CurrentPlayer.User, PresidentCard.QUEEN, 1);
        var follower = _game.CurrentPlayer;

        await _game.PlayAsync(follower.User, 3, 1);

        using (Assert.EnterMultipleScope())
        {
            _context.Received(1).ReplyLocalizedMessage("president_play_too_low");
            Assert.That(_game.CurrentPlayer, Is.EqualTo(follower));
            Assert.That(_game.CurrentTrick.Plays, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_Play_ShouldBeRejected_WhenCountDoesNotMatchThePile()
    {
        await JoinAndStartAsync(3);
        // player1 leads a pair of 3s; player2 then tries a single king.
        await _game.PlayAsync(_game.CurrentPlayer.User, 3, 2);
        var follower = _game.CurrentPlayer;

        await _game.PlayAsync(follower.User, PresidentCard.KING, 1);

        using (Assert.EnterMultipleScope())
        {
            _context.Received(1).ReplyLocalizedMessage("president_play_wrong_count", Arg.Any<object[]>());
            Assert.That(_game.CurrentPlayer, Is.EqualTo(follower));
        }
    }

    [Test]
    public async Task Test_Play_ShouldTakeThePileImmediately_WhenATwoIsPlayed()
    {
        await JoinAndStartAsync(3);
        var leader = _game.CurrentPlayer;

        await _game.PlayAsync(leader.User, PresidentCard.TWO, 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.CurrentTrick.IsEmpty, Is.True);
            Assert.That(_game.LastTrickWinner, Is.EqualTo(leader));
            Assert.That(_game.CurrentPlayer, Is.EqualTo(leader));
            Assert.That(_game.Log, Does.Contain("president_trick_won"));
        }
    }

    [Test]
    public async Task Test_Pass_ShouldBeRejected_WhenLeadingTheTrick()
    {
        await JoinAndStartAsync(3);
        var leader = _game.CurrentPlayer;

        await _game.PassAsync(leader.User);

        using (Assert.EnterMultipleScope())
        {
            _context.Received(1).ReplyLocalizedMessage("president_pass_leading");
            Assert.That(_game.CurrentPlayer, Is.EqualTo(leader));
        }
    }

    [Test]
    public async Task Test_Pass_ShouldCloseTheTrick_WhenEveryoneElsePasses()
    {
        await JoinAndStartAsync(3);
        var leader = _game.CurrentPlayer;

        await _game.PlayAsync(leader.User, 3, 1);
        await _game.PassAsync(_game.CurrentPlayer.User);
        await _game.PassAsync(_game.CurrentPlayer.User);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.CurrentTrick.IsEmpty, Is.True);
            Assert.That(_game.LastTrickWinner, Is.EqualTo(leader));
            Assert.That(_game.CurrentPlayer, Is.EqualTo(leader));
            Assert.That(_game.Players.All(player => !player.HasPassed), Is.True);
        }
    }

    [TestCase(3)]
    [TestCase(5)]
    public async Task Test_FullRound_ShouldAssignRolesAndPoints_WhenLastRoundEnds(int players)
    {
        await JoinAndStartAsync(players);
        _game.TotalRounds = 1;

        await DriveRoundAsync();

        var byPosition = _game.Players.OrderBy(player => player.FinishPosition).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Finished));
            Assert.That(_game.IsEnded, Is.True);
            Assert.That(_game.Players.Select(player => player.FinishPosition),
                Is.EquivalentTo(Enumerable.Range(1, players)));
            Assert.That(byPosition[0].Role, Is.EqualTo(PresidentRole.President));
            Assert.That(byPosition[0].Score, Is.EqualTo(players - 1));
            Assert.That(byPosition[^1].Role, Is.EqualTo(PresidentRole.Scum));
            Assert.That(byPosition[^1].Score, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Test_FullRound_ShouldAssignViceRoles_WhenAtLeastFourPlayers()
    {
        await JoinAndStartAsync(5);
        _game.TotalRounds = 1;

        await DriveRoundAsync();

        var byPosition = _game.Players.OrderBy(player => player.FinishPosition).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(byPosition[1].Role, Is.EqualTo(PresidentRole.VicePresident));
            Assert.That(byPosition[2].Role, Is.EqualTo(PresidentRole.Neutral));
            Assert.That(byPosition[^2].Role, Is.EqualTo(PresidentRole.ViceScum));
        }
    }

    [Test]
    public async Task Test_RoundEnd_ShouldOpenExchange_WhenAnotherRoundFollows()
    {
        await JoinAndStartAsync(3);
        _game.TotalRounds = 2;

        await DriveRoundAsync();

        var president = _game.Players.Single(player => player.Role == PresidentRole.President);
        var scum = _game.Players.Single(player => player.Role == PresidentRole.Scum);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Exchange));
            Assert.That(_game.RoundNumber, Is.EqualTo(2));
            Assert.That(_game.Players.Sum(player => player.Hand.Count), Is.EqualTo(52));
            Assert.That(president.ReceivedCards, Has.Count.EqualTo(PresidentConstants.SCUM_EXCHANGE_COUNT));
            Assert.That(president.CardsToGive, Is.EqualTo(PresidentConstants.SCUM_EXCHANGE_COUNT));
            Assert.That(scum.CardsToGive, Is.Zero);
            Assert.That(_game.Log, Does.Contain("president_exchange_gave_best"));
        }
    }

    [Test]
    public async Task Test_Give_ShouldCompleteExchangeAndLetPresidentLead()
    {
        await JoinAndStartAsync(3);
        _game.TotalRounds = 2;
        await DriveRoundAsync();

        var president = _game.Players.Single(player => player.Role == PresidentRole.President);
        var scum = _game.Players.Single(player => player.Role == PresidentRole.Scum);
        var lowestCards = president.Hand.OrderBy(card => card.Rank)
            .Take(PresidentConstants.SCUM_EXCHANGE_COUNT).ToList();

        await _game.GiveAsync(president.User, lowestCards);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Playing));
            Assert.That(_game.CurrentPlayer, Is.EqualTo(president));
            Assert.That(scum.ReceivedCards, Is.EquivalentTo(lowestCards));
            Assert.That(_game.Players.Sum(player => player.Hand.Count), Is.EqualTo(52));
            Assert.That(_game.Log, Does.Contain("president_exchange_returned"));
        }
    }

    [Test]
    public async Task Test_Give_ShouldToggleSelection_WhenSingleCardIsSent()
    {
        await JoinAndStartAsync(3);
        _game.TotalRounds = 2;
        await DriveRoundAsync();

        var president = _game.Players.Single(player => player.Role == PresidentRole.President);
        var card = president.Hand[0];

        await _game.GiveAsync(president.User, [card]);
        Assert.That(president.PendingGives, Is.EqualTo(new[] { card }));

        await _game.GiveAsync(president.User, [card]);
        Assert.That(president.PendingGives, Is.Empty);
    }

    [Test]
    public async Task Test_Cancel_ShouldEndTheGame()
    {
        await JoinAndStartAsync(3);

        await _game.CancelAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.Phase, Is.EqualTo(PresidentPhase.Finished));
            Assert.That(_game.IsEnded, Is.True);
        }
    }
}
