using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.Wordle;
using ElsaMina.Commands.RoomDashboard;
using ElsaMina.Commands.Tournaments.Betting;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.RoomDashboard;

[TestFixture]
public class RoomConfigCommandTest
{
    private IContext _context;
    private IRoomsManager _roomsManager;
    private IParametersDefinitionFactory _parametersDefinitionFactory;
    private IArcadeEventsService _arcadeEventsService;
    private ITournamentBettingService _tournamentBettingService;
    private IRoomDashboardService _roomDashboardService;
    private RoomConfigCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _parametersDefinitionFactory = Substitute.For<IParametersDefinitionFactory>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _tournamentBettingService = Substitute.For<ITournamentBettingService>();
        _roomDashboardService = Substitute.For<IRoomDashboardService>();

        _command = new RoomConfigCommand(
            _roomsManager,
            _parametersDefinitionFactory,
            _arcadeEventsService,
            _tournamentBettingService,
            _roomDashboardService);

        _parametersDefinitionFactory.GetParametersDefinitions()
            .Returns(new Dictionary<Parameter, IParameterDefinition>());
        _context.HasSufficientRankInRoom(Arg.Any<string>(), Arg.Any<Rank>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenTargetIsEmpty()
    {
        _context.Target.Returns("  ");

        await _command.RunAsync(_context);

        _roomsManager.DidNotReceive().GetRoom(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeny_WhenUserHasInsufficientRank()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Locale=fr-FR");
        _roomsManager.GetRoom("testroom").Returns(room);
        _context.HasSufficientRankInRoom("testroom", Rank.Driver, Arg.Any<CancellationToken>())
            .Returns(false);

        await _command.RunAsync(_context);

        await room.DidNotReceive()
            .SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _roomDashboardService.DidNotReceive()
            .SendDashboardPageAsync(Arg.Any<IContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("unknownroom,Locale=en-US");
        _roomsManager.GetRoom("unknownroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("room_config_room_not_found", "unknownroom");
        await _roomDashboardService.DidNotReceive()
            .SendDashboardPageAsync(Arg.Any<IContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldUpdateParameters_WhenInputIsValid()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Locale=fr-FR");
        _roomsManager.GetRoom("testroom").Returns(room);

        var paramDef = Substitute.For<IParameterDefinition>();
        paramDef.Identifier.Returns("loc");
        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, paramDef }
        });
        room.SetParameterValueAsync(Parameter.Locale, "fr-FR", Arg.Any<CancellationToken>()).Returns(true);

        await _command.RunAsync(_context);

        await room.Received(1).SetParameterValueAsync(Parameter.Locale, "fr-FR", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("room_config_success", "testroom");
        await _roomDashboardService.Received(1).SendDashboardPageAsync(_context, "testroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldUpdateParameters_WhenIdentifierMatches()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,loc=fr-FR");
        _roomsManager.GetRoom("testroom").Returns(room);

        var paramDef = Substitute.For<IParameterDefinition>();
        paramDef.Identifier.Returns("loc");
        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, paramDef }
        });
        room.SetParameterValueAsync(Parameter.Locale, "fr-FR", Arg.Any<CancellationToken>()).Returns(true);

        await _command.RunAsync(_context);

        await room.Received(1).SetParameterValueAsync(Parameter.Locale, "fr-FR", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("room_config_success", "testroom");
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldReject_WhenParameterOrValueIsInvalid()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Unknown=foo");
        _roomsManager.GetRoom("testroom").Returns(room);

        var localeDef = Substitute.For<IParameterDefinition>();
        localeDef.Identifier.Returns("loc");
        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, localeDef }
        });

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("room_config_unknown_parameter", "Unknown");
        await room.DidNotReceive()
            .SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _context.DidNotReceive().ReplyLocalizedMessage("room_config_success", Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidValue_WhenSetParameterValueReturnsFalse()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Locale=invalid");
        _roomsManager.GetRoom("testroom").Returns(room);

        var localeDef = Substitute.For<IParameterDefinition>();
        localeDef.Identifier.Returns("loc");
        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, localeDef }
        });
        room.SetParameterValueAsync(Parameter.Locale, "invalid", Arg.Any<CancellationToken>()).Returns(false);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("room_config_invalid_value", "invalid", "Locale");
        _context.DidNotReceive().ReplyLocalizedMessage("room_config_success", Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidPair_WhenPairHasNoValue()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Locale");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("room_config_invalid_pair", "Locale");
        await room.DidNotReceive()
            .SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldToggleMuteGames_WhenQuickActionTriggered()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,mutegames");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        _arcadeEventsService.Received(1).MuteGames("testroom", TimeSpan.FromMinutes(30));
        _context.Received(1).ReplyLocalizedMessage("room_config_success", "testroom");
        await _roomDashboardService.Received(1).SendDashboardPageAsync(_context, "testroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldUnmuteGames_WhenUnmuteQuickActionTriggered()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,unmutegames");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        _arcadeEventsService.Received(1).UnmuteGames("testroom");
        _context.Received(1).ReplyLocalizedMessage("room_config_success", "testroom");
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldHandleActionParameter_WhenMuteGamesActionSpecified()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,action=mutegames");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        _arcadeEventsService.Received(1).MuteGames("testroom", TimeSpan.FromMinutes(30));
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldHandleActionParameter_WhenUnmuteGamesActionSpecified()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,action=unmutegames");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        _arcadeEventsService.Received(1).UnmuteGames("testroom");
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldHandleActionParameter_WhenToggleGamesActionSpecified()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,togglegames");
        _roomsManager.GetRoom("testroom").Returns(room);
        _arcadeEventsService.AreGamesMuted("testroom").Returns(true);

        await _command.RunAsync(_context);

        _arcadeEventsService.Received(1).UnmuteGames("testroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallSetParameterValue_ForEachPairInTarget()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Locale=fr-FR,TimeZone=Europe/Paris");
        _roomsManager.GetRoom("testroom").Returns(room);

        var localeDef = Substitute.For<IParameterDefinition>();
        localeDef.Identifier.Returns("loc");
        var timeZoneDef = Substitute.For<IParameterDefinition>();
        timeZoneDef.Identifier.Returns("tzn");
        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, localeDef },
            { Parameter.TimeZone, timeZoneDef }
        });
        room.SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _command.RunAsync(_context);

        await room.Received(1).SetParameterValueAsync(Parameter.Locale, "fr-FR", Arg.Any<CancellationToken>());
        await room.Received(1).SetParameterValueAsync(Parameter.TimeZone, "Europe/Paris", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("room_config_success", "testroom");
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldCancelActiveGame_WhenCancelGameQuickActionTriggered()
    {
        var room = Substitute.For<IRoom>();
        var wordleGame = Substitute.For<IWordleGame>();
        room.Game.Returns(wordleGame);
        _context.Target.Returns("testroom,cancelgame");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        await wordleGame.Received(1).CancelAsync();
        _context.Received(1).ReplyLocalizedMessage("room_config_success", "testroom");
        await _roomDashboardService.Received(1).SendDashboardPageAsync(_context, "testroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldReturnBets_WhenCancelBetsQuickActionTriggered()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,cancelbets");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        await _tournamentBettingService.Received(1).ReturnBetsAsync("testroom", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("room_config_success", "testroom");
        await _roomDashboardService.Received(1).SendDashboardPageAsync(_context, "testroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RoomConfigCommand_ShouldResetParametersToDefaults_WhenResetQuickActionTriggered()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,reset");
        _roomsManager.GetRoom("testroom").Returns(room);

        var localeDef = Substitute.For<IParameterDefinition>();
        localeDef.DefaultValue.Returns("en-US");
        var bucksDef = Substitute.For<IParameterDefinition>();
        bucksDef.DefaultValue.Returns("true");

        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, localeDef },
            { Parameter.BucksEnabled, bucksDef }
        });

        await _command.RunAsync(_context);

        await room.Received(1).SetParameterValueAsync(Parameter.Locale, "en-US", Arg.Any<CancellationToken>());
        await room.Received(1).SetParameterValueAsync(Parameter.BucksEnabled, "true", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("room_config_success", "testroom");
        await _roomDashboardService.Received(1).SendDashboardPageAsync(_context, "testroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFailure_WhenExceptionIsThrown()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Locale=fr-FR");
        _roomsManager.GetRoom("testroom").Returns(room);

        var paramDef = Substitute.For<IParameterDefinition>();
        paramDef.Identifier.Returns("loc");
        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, paramDef }
        });
        room.SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new Exception("db error")));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("room_config_failure", "db error");
    }
}
