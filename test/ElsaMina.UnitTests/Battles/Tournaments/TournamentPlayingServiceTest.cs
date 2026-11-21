using ElsaMina.Battles;
using ElsaMina.Battles.Tournaments;
using ElsaMina.Core;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.System;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Tournaments;

[TestFixture]
public class TournamentPlayingServiceTest
{
    private const string ROOM = "randbats";
    private const string READY = "/tour acceptchallenge";

    // What Showdown sends when a tournament is created or the bot joins its room: its updates come right away, its
    // |create| with the room's next batch
    private const string SIGNUPS_UPDATE = """|tournament|update|{"format":"[Gen 9] Random Battle","teambuilderFormat":"gen9randombattle","generator":"Single Elimination","playerCap":0,"isStarted":false,"isJoined":false}""";
    private const string BRACKET_UPDATE = """|tournament|update|{"bracketData":{"type":"tree","rootNode":null}}""";
    private const string UPDATE_END = "|tournament|updateEnd";
    private const string CREATE = "|tournament|create|gen9randombattle|Single Elimination|0";
    private const string STARTED_UPDATE = """|tournament|update|{"format":"[Gen 9] Random Battle","teambuilderFormat":"gen9randombattle","generator":"Single Elimination","isStarted":true,"isJoined":true}""";
    private const string NEXT_OPPONENT_UPDATE = """|tournament|update|{"challenges":[],"challengeBys":[],"challenged":"Opponent","challenging":null}""";
    private const string READY_UPDATE = """|tournament|update|{"challenges":[],"challengeBys":[],"challenged":null,"challenging":"Opponent"}""";

    private IBot _bot;
    private IBattleTeamSender _teamSender;
    private IRoom _room;
    private ISystemService _systemService;
    private TournamentPlayingService _service;
    private readonly List<(TimeSpan Delay, TaskCompletionSource Completion)> _sleeps = [];

    [SetUp]
    public void SetUp()
    {
        _bot = Substitute.For<IBot>();
        _teamSender = Substitute.For<IBattleTeamSender>();
        _room = Substitute.For<IRoom>();
        SetAutoJoin(true);
        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.GetRoom(ROOM).Returns(_room);
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa Mina");
        _systemService = Substitute.For<ISystemService>();
        _sleeps.Clear();
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            var completion = new TaskCompletionSource();
            callInfo.Arg<CancellationToken>().Register(() => completion.TrySetCanceled());
            _sleeps.Add((callInfo.Arg<TimeSpan>(), completion));
            return completion.Task;
        });
        _service = new TournamentPlayingService(_bot, _teamSender, roomsManager, configuration, _systemService);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    private void SetAutoJoin(bool isEnabled)
    {
        _room.GetParameterValueAsync(TournamentsRoomParameters.AutoJoinTournaments, Arg.Any<CancellationToken>())
            .Returns(isEnabled.ToString());
    }

    private async Task ReceiveAsync(params string[] lines)
    {
        foreach (var line in lines)
        {
            await _service.HandleMessageAsync(line.Split('|'), ROOM);
        }
    }

    private Task StartTournamentAsync() => ReceiveAsync(STARTED_UPDATE, UPDATE_END);

    // Completed from the thread pool, where the service's continuation runs inline before Task.Run ends
    private async Task ElapseLastSleepAsync()
    {
        var (_, completion) = _sleeps[^1];
        await Task.Run(() => completion.TrySetResult());
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldJoinOnce_WhenSupportedTournamentIsCreatedInAutoJoinRoom()
    {
        // Act
        await ReceiveAsync(SIGNUPS_UPDATE, BRACKET_UPDATE, UPDATE_END, CREATE);

        // Assert
        _bot.Received(1).Say(ROOM, "/tour join");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldJoinOnce_WhenCreateComesBeforeUpdates()
    {
        // Act
        await ReceiveAsync(CREATE, SIGNUPS_UPDATE, BRACKET_UPDATE, UPDATE_END);

        // Assert
        _bot.Received(1).Say(ROOM, "/tour join");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldJoin_WhenBotJoinsRoomDuringSignups()
    {
        // Act
        await ReceiveAsync("|init|chat", SIGNUPS_UPDATE, UPDATE_END);

        // Assert
        _bot.Received(1).Say(ROOM, "/tour join");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotJoin_WhenAutoJoinIsDisabled()
    {
        // Arrange
        SetAutoJoin(false);

        // Act
        await ReceiveAsync(SIGNUPS_UPDATE, UPDATE_END, CREATE);

        // Assert
        _bot.DidNotReceive().Say(ROOM, "/tour join");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotJoin_WhenFormatIsNotSupported()
    {
        // Act
        await ReceiveAsync(
            """|tournament|update|{"format":"[Gen 9] OU","teambuilderFormat":"gen9ou","isStarted":false,"isJoined":false}""",
            UPDATE_END, "|tournament|create|gen9ou|Single Elimination|0");

        // Assert
        _bot.DidNotReceive().Say(ROOM, "/tour join");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotJoin_WhenTournamentHasStarted()
    {
        // Act
        await ReceiveAsync(
            """|tournament|update|{"format":"[Gen 9] Random Battle","teambuilderFormat":"gen9randombattle","isStarted":true,"isJoined":false}""",
            UPDATE_END);

        // Assert
        _bot.DidNotReceive().Say(ROOM, "/tour join");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotJoinAgain_WhenBotLeftTournament()
    {
        // Arrange
        await ReceiveAsync(SIGNUPS_UPDATE, UPDATE_END, """|tournament|update|{"isJoined":true}""", UPDATE_END);
        _service.Leave(ROOM);

        // Act
        await ReceiveAsync("""|tournament|update|{"isJoined":false}""", UPDATE_END);

        // Assert
        _bot.Received(1).Say(ROOM, "/tour join");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldReady_WhenServerGivesNextOpponent()
    {
        // Arrange
        await StartTournamentAsync();

        // Act
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END);

        // Assert
        _teamSender.Received(1).SendWithTeam(null, READY, ROOM);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldReady_WhenNextOpponentIsNotKnownYet()
    {
        // Arrange
        await StartTournamentAsync();

        // Act
        await ReceiveAsync(
            """|tournament|update|{"challenges":[],"challengeBys":[],"challenged":"(next available player)","challenging":null}""",
            UPDATE_END);

        // Assert
        _teamSender.Received(1).SendWithTeam(null, READY, ROOM);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotReadyAgain_WhenServerHasNotAnsweredYet()
    {
        // Arrange
        await StartTournamentAsync();

        // Act
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END, BRACKET_UPDATE, UPDATE_END);

        // Assert
        _teamSender.Received(1).SendWithTeam(null, READY, ROOM);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldReadyAgainLaterEachTime_WhenServerDoesNotAnswer()
    {
        // Arrange
        await StartTournamentAsync();
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END);

        // Act
        await ElapseLastSleepAsync();
        await ElapseLastSleepAsync();

        // Assert
        _teamSender.Received(3).SendWithTeam(null, READY, ROOM);
        Assert.That(_sleeps.Select(sleep => sleep.Delay), Is.EqualTo(new[]
        {
            TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)
        }));
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldStopRetrying_WhenBotIsReady()
    {
        // Arrange
        await StartTournamentAsync();
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END);

        // Act
        await ReceiveAsync(READY_UPDATE, UPDATE_END);

        // Assert
        Assert.That(_sleeps[^1].Completion.Task.IsCanceled, Is.True);
        _teamSender.Received(1).SendWithTeam(null, READY, ROOM);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldReadyAtOnce_WhenServerWarnsOfDisqualification()
    {
        // Arrange
        await StartTournamentAsync();
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END);

        // Act
        await ReceiveAsync("|tournament|autodq|target|30000");

        // Assert
        _teamSender.Received(2).SendWithTeam(null, READY, ROOM);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldReadyForNextMatch_WhenBattleEnds()
    {
        // Arrange
        await StartTournamentAsync();
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END, READY_UPDATE, UPDATE_END,
            "|tournament|battlestart|Elsa Mina|Opponent|battle-gen9randombattle-1",
            """|tournament|update|{"challenges":[],"challengeBys":[],"challenged":null,"challenging":null}""",
            UPDATE_END);

        // Act
        await ReceiveAsync(
            "|tournament|battleend|Elsa Mina|Opponent|win|3,0|success|battle-gen9randombattle-1",
            """|tournament|update|{"challenges":[],"challengeBys":[],"challenged":"Rival","challenging":null}""",
            UPDATE_END);

        // Assert
        _teamSender.Received(2).SendWithTeam(null, READY, ROOM);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldStopRetrying_WhenBattleStarts()
    {
        // Arrange
        await StartTournamentAsync();
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END);

        // Act
        await ReceiveAsync("|tournament|battlestart|Opponent|Elsa Mina|battle-gen9randombattle-1");

        // Assert
        Assert.That(_sleeps[^1].Completion.Task.IsCanceled, Is.True);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotStopRetrying_WhenOtherPlayersBattleStarts()
    {
        // Arrange
        await StartTournamentAsync();
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END);

        // Act
        await ReceiveAsync("|tournament|battlestart|Someone|Else|battle-gen9randombattle-2");

        // Assert
        Assert.That(_sleeps[^1].Completion.Task.IsCanceled, Is.False);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldChallenge_WhenOlderServerListsOpponents()
    {
        // Arrange
        await StartTournamentAsync();

        // Act
        await ReceiveAsync("""|tournament|update|{"challenges":["Opponent"]}""",
            """|tournament|update|{"challengeBys":[]}""", UPDATE_END);

        // Assert
        _teamSender.Received(1).SendWithTeam(null, "/tour challenge Opponent", ROOM);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldWaitBeforeChallengingAgain_WhenOlderServerRefusesChallenge()
    {
        // Arrange: the server lists no opponent while it validates the challenge, then the same one when it fails
        await StartTournamentAsync();
        await ReceiveAsync("""|tournament|update|{"challenges":["Opponent"]}""", UPDATE_END);
        await ReceiveAsync("""|tournament|update|{"challenges":[]}""", UPDATE_END);
        await ReceiveAsync("""|tournament|update|{"challenges":["Opponent"]}""", UPDATE_END);
        _teamSender.Received(1).SendWithTeam(null, "/tour challenge Opponent", ROOM);

        // Act
        await ElapseLastSleepAsync();

        // Assert
        _teamSender.Received(2).SendWithTeam(null, "/tour challenge Opponent", ROOM);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotChallenge_WhenOlderServerSaysBotIsChallenging()
    {
        // Arrange
        await StartTournamentAsync();
        await ReceiveAsync("""|tournament|update|{"challenges":["Opponent"]}""", UPDATE_END);

        // Act
        await ReceiveAsync("""|tournament|update|{"challenging":"Opponent"}""", UPDATE_END);
        await ElapseLastSleepAsync();

        // Assert
        _teamSender.Received(1).SendWithTeam(null, "/tour challenge Opponent", ROOM);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldAccept_WhenOlderServerSaysBotIsChallenged()
    {
        // Arrange
        await StartTournamentAsync();

        // Act
        await ReceiveAsync("""|tournament|update|{"challenged":"Opponent"}""", UPDATE_END);

        // Assert
        _teamSender.Received(1).SendWithTeam(null, READY, ROOM);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotPlay_WhenFormatIsNotSupported()
    {
        // Arrange
        await ReceiveAsync(
            """|tournament|update|{"format":"[Gen 9] OU","teambuilderFormat":"gen9ou","isStarted":true,"isJoined":true}""",
            UPDATE_END);

        // Act
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END);

        // Assert
        _teamSender.DidNotReceiveWithAnyArgs().SendWithTeam(null, null, null);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldKeepFormat_WhenTournamentIsRenamed()
    {
        // Arrange: custom rules rename the tournament, without saying its format again
        SetAutoJoin(false);
        await ReceiveAsync(SIGNUPS_UPDATE, UPDATE_END);

        // Act
        await ReceiveAsync("""|tournament|update|{"format":"[Gen 9] Random Battle (with custom rules)"}""",
            UPDATE_END);

        // Assert
        Assert.That(_service.Join(ROOM), Is.EqualTo(TournamentJoinResult.Joining));
    }

    [TestCase("|tournament|end|{\"results\":[[\"Opponent\"]],\"format\":\"gen9randombattle\"}")]
    [TestCase("|tournament|forceend")]
    [TestCase("|deinit")]
    [TestCase("|init|chat")]
    public async Task Test_HandleMessageAsync_ShouldForgetTournament_WhenItEndsOrBotLeavesRoom(string line)
    {
        // Arrange
        await StartTournamentAsync();
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END);

        // Act
        await ReceiveAsync(line);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_service.Join(ROOM), Is.EqualTo(TournamentJoinResult.NoTournament));
            Assert.That(_sleeps[^1].Completion.Task.IsCanceled, Is.True);
        }
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldIgnoreUpdate_WhenItIsNotJson()
    {
        // Act
        await ReceiveAsync("|tournament|update|not json", UPDATE_END);

        // Assert
        Assert.That(_service.Join(ROOM), Is.EqualTo(TournamentJoinResult.NoTournament));
    }

    [Test]
    public void Test_Join_ShouldSayThereIsNoTournament_WhenRoomHasNone()
    {
        // Act
        var result = _service.Join(ROOM);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(TournamentJoinResult.NoTournament));
            _bot.DidNotReceiveWithAnyArgs().Say(null, null);
        }
    }

    [Test]
    public async Task Test_Join_ShouldJoin_WhenTournamentIsInSignups()
    {
        // Arrange
        SetAutoJoin(false);
        await ReceiveAsync(SIGNUPS_UPDATE, UPDATE_END);

        // Act
        var result = _service.Join(ROOM);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(TournamentJoinResult.Joining));
            _bot.Received(1).Say(ROOM, "/tour join");
        }
    }

    [Test]
    public async Task Test_Join_ShouldSayBotIsAlreadyIn_WhenJoined()
    {
        // Arrange
        await StartTournamentAsync();

        // Act
        var result = _service.Join(ROOM);

        // Assert
        Assert.That(result, Is.EqualTo(TournamentJoinResult.AlreadyJoined));
    }

    [Test]
    public async Task Test_Join_ShouldSayTournamentStarted_WhenStartedWithoutBot()
    {
        // Arrange
        await ReceiveAsync(
            """|tournament|update|{"format":"[Gen 9] Random Battle","teambuilderFormat":"gen9randombattle","isStarted":true,"isJoined":false}""");

        // Act
        var result = _service.Join(ROOM);

        // Assert
        Assert.That(result, Is.EqualTo(TournamentJoinResult.AlreadyStarted));
    }

    [Test]
    public async Task Test_Join_ShouldSayFormatIsNotSupported_WhenBotCannotPlayIt()
    {
        // Arrange
        await ReceiveAsync("|tournament|create|gen9ou|Single Elimination|0");

        // Act
        var result = _service.Join(ROOM);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(TournamentJoinResult.UnsupportedFormat));
            _bot.DidNotReceive().Say(ROOM, "/tour join");
        }
    }

    [Test]
    public void Test_Leave_ShouldDoNothing_WhenBotIsNotInTournament()
    {
        // Act
        var isLeaving = _service.Leave(ROOM);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(isLeaving, Is.False);
            _bot.DidNotReceiveWithAnyArgs().Say(null, null);
        }
    }

    [Test]
    public async Task Test_Leave_ShouldLeaveAndStopRetrying_WhenBotIsInTournament()
    {
        // Arrange
        await StartTournamentAsync();
        await ReceiveAsync(NEXT_OPPONENT_UPDATE, UPDATE_END);

        // Act
        var isLeaving = _service.Leave(ROOM);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(isLeaving, Is.True);
            _bot.Received(1).Say(ROOM, "/tour leave");
            Assert.That(_sleeps[^1].Completion.Task.IsCanceled, Is.True);
        }
    }
}
