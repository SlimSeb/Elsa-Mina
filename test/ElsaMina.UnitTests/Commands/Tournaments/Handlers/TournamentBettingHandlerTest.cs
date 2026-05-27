using ElsaMina.Commands.Tournaments.Betting;
using ElsaMina.Commands.Tournaments.Handlers;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;
using NUnit.Framework;

namespace ElsaMina.UnitTests.Commands.Tournaments.Handlers;

[TestFixture]
public class TournamentBettingHandlerTest
{
    private const string USERS_JSON =
        """{"bracketData":{"type":"tree","rootNode":null,"users":["PlayerA","PlayerB"]}}""";

    private const string DUPLICATE_USERS_JSON =
        """{"bracketData":{"type":"tree","rootNode":null,"users":["PlayerA","player a","PlayerB"]}}""";

    private const string STARTED_JSON =
        """{"isStarted":true}""";

    private const string BRACKET_NO_USERS_JSON =
        """{"bracketData":{"type":"tree","rootNode":{"children":[{"team":"PlayerA"},{"team":"PlayerB"}],"state":"available"}}}""";

    private const string TOUR_END_JSON =
        """{"results":[["PlayerA"]],"format":"OU","generator":"Single Elimination","bracketData":{"type":"tree","rootNode":{"children":[{"team":"PlayerA"},{"team":"PlayerB"}],"state":"finished","team":"PlayerA","result":"win","score":[1,0]}}}""";

    private ITournamentBettingService _bettingService;
    private IRoomsManager _roomsManager;
    private IRoom _room;
    private TournamentBettingHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _bettingService = Substitute.For<ITournamentBettingService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _room = Substitute.For<IRoom>();

        _room.GetParameterValueAsync(Parameter.TournamentBettingEnabled, Arg.Any<CancellationToken>())
            .Returns(true.ToString());
        _roomsManager.GetRoom(Arg.Any<string>()).Returns(_room);

        _handler = new TournamentBettingHandler(_bettingService, _roomsManager);
    }

    // ── guard clauses ──────────────────────────────────────────────────────

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenRoomIdIsNull()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", USERS_JSON], null);

        await _bettingService.DidNotReceive().AnnounceBetsAsync(Arg.Any<TournamentPlayer[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDoNothing_WhenPartsAreTooShort()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament"], "room1");

        await _bettingService.DidNotReceive().AnnounceBetsAsync(Arg.Any<TournamentPlayer[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── update ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldStorePlayers_WhenUpdateHasUsers()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", USERS_JSON], "room1");
        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        await _bettingService.Received(1).AnnounceBetsAsync(
            Arg.Is<TournamentPlayer[]>(p => p.Any(player => player.UserName == "PlayerA") && p.Any(player => player.UserName == "PlayerB")),
            "room1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotOverwritePlayers_WhenUpdateHasNoUsers()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", USERS_JSON], "room1");
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", STARTED_JSON], "room1");
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", BRACKET_NO_USERS_JSON], "room1");
        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        await _bettingService.Received(1).AnnounceBetsAsync(
            Arg.Is<TournamentPlayer[]>(p => p.Any(player => player.UserName == "PlayerA") && p.Any(player => player.UserName == "PlayerB")),
            "room1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldDeduplicatePlayers_ByNormalizedUserId()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", DUPLICATE_USERS_JSON], "room1");
        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        await _bettingService.Received(1).AnnounceBetsAsync(
            Arg.Is<TournamentPlayer[]>(p =>
                p.Length == 2 &&
                p.Count(player => player.UserId == "playera") == 1 &&
                p.Any(player => player.UserId == "playerb")),
            "room1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnoreUpdate_WhenPartsAreTooShort()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update"], "room1");
        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        await _bettingService.DidNotReceive().AnnounceBetsAsync(Arg.Any<TournamentPlayer[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── start ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAnnounce_WhenStartReceivedAfterUpdate()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", USERS_JSON], "room1");

        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        await _bettingService.Received(1).AnnounceBetsAsync(Arg.Any<TournamentPlayer[]>(), "room1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotAnnounce_WhenStartReceivedWithoutPriorUpdate()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        await _bettingService.DidNotReceive().AnnounceBetsAsync(Arg.Any<TournamentPlayer[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldClearPendingPlayers_AfterStart()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", USERS_JSON], "room1");
        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        // A second start must not announce again
        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        await _bettingService.Received(1).AnnounceBetsAsync(Arg.Any<TournamentPlayer[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldBeIsolatedByRoom()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", USERS_JSON], "room1");

        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room2");

        await _bettingService.DidNotReceive().AnnounceBetsAsync(Arg.Any<TournamentPlayer[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotAnnounce_WhenBettingDisabled()
    {
        _room.GetParameterValueAsync(Parameter.TournamentBettingEnabled, Arg.Any<CancellationToken>())
            .Returns(false.ToString());

        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", USERS_JSON], "room1");
        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        await _bettingService.DidNotReceive().AnnounceBetsAsync(Arg.Any<TournamentPlayer[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldAnnounce_WhenRoomNotFound()
    {
        _roomsManager.GetRoom("room1").Returns((IRoom)null);

        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", USERS_JSON], "room1");
        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        await _bettingService.Received(1).AnnounceBetsAsync(Arg.Any<TournamentPlayer[]>(), "room1", Arg.Any<CancellationToken>());
    }

    // ── end ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldResolveBets_WhenEndWithValidJson()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "end", TOUR_END_JSON], "room1");

        await _bettingService.Received(1).ResolveBetsAsync("playera", "room1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldReturnBets_WhenEndJsonIsNotSingleElim()
    {
        var doubleElimJson = TOUR_END_JSON.Replace("Single Elimination", "Double Elimination");

        await _handler.HandleReceivedMessageAsync(["", "tournament", "end", doubleElimJson], "room1");

        await _bettingService.Received(1).ReturnBetsAsync("room1", Arg.Any<CancellationToken>());
        await _bettingService.DidNotReceive().ResolveBetsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldReturnBets_WhenEndJsonIsInvalid()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "end", "not-json"], "room1");

        await _bettingService.Received(1).ReturnBetsAsync("room1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnoreEnd_WhenPartsAreTooShort()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "end"], "room1");

        await _bettingService.DidNotReceive().ResolveBetsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _bettingService.DidNotReceive().ReturnBetsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── forceend ───────────────────────────────────────────────────────────

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldReturnBets_WhenForceEnd()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "forceend"], "room1");

        await _bettingService.Received(1).ReturnBetsAsync("room1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldClearPendingPlayers_OnForceEnd()
    {
        await _handler.HandleReceivedMessageAsync(["", "tournament", "update", USERS_JSON], "room1");

        await _handler.HandleReceivedMessageAsync(["", "tournament", "forceend"], "room1");
        await _handler.HandleReceivedMessageAsync(["", "tournament", "start"], "room1");

        await _bettingService.DidNotReceive().AnnounceBetsAsync(Arg.Any<TournamentPlayer[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
