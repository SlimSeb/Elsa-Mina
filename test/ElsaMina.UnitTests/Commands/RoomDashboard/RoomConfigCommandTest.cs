using ElsaMina.Commands.RoomDashboard;
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
    private RoomConfigCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _parametersDefinitionFactory = Substitute.For<IParametersDefinitionFactory>();
        _command = new RoomConfigCommand(_roomsManager, _parametersDefinitionFactory);

        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>());
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        Assert.That(_command.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public void Test_IsWhitelistOnly_ShouldBeTrue()
    {
        Assert.That(_command.IsWhitelistOnly, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("unknownroom,Locale=en-US");
        _roomsManager.GetRoom("unknownroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("room_config_room_not_found", "unknownroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotCallSetParameterValue_WhenRoomDoesNotExist()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("unknownroom,Locale=en-US");
        _roomsManager.GetRoom("unknownroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        await room.DidNotReceive().SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySuccess_WhenParametersAreUpdated()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Locale=fr-FR");
        _roomsManager.GetRoom("testroom").Returns(room);

        var paramDef = Substitute.For<IParameterDefinition>();
        paramDef.Identifier.Returns("Locale");
        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, paramDef }
        });
        room.SetParameterValueAsync(Parameter.Locale, "fr-FR", Arg.Any<CancellationToken>()).Returns(true);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("room_config_success", "testroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallSetParameterValue_ForEachPairInTarget()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Locale=fr-FR,TimeZone=Europe/Paris");
        _roomsManager.GetRoom("testroom").Returns(room);

        var localeDef = Substitute.For<IParameterDefinition>();
        localeDef.Identifier.Returns("Locale");
        var timeZoneDef = Substitute.For<IParameterDefinition>();
        timeZoneDef.Identifier.Returns("TimeZone");
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
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUnknownParameter_AndNotSetValue_WhenIdentifierDoesNotMatch()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Unknown=foo");
        _roomsManager.GetRoom("testroom").Returns(room);

        var localeDef = Substitute.For<IParameterDefinition>();
        localeDef.Identifier.Returns("Locale");
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
        localeDef.Identifier.Returns("Locale");
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

        var localeDef = Substitute.For<IParameterDefinition>();
        localeDef.Identifier.Returns("Locale");
        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, localeDef }
        });

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("room_config_invalid_pair", "Locale");
        await room.DidNotReceive()
            .SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimRoomId_WhenTargetHasWhitespace()
    {
        _context.Target.Returns("  testroom  ,Locale=en-US");
        _roomsManager.GetRoom("testroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        _roomsManager.Received(1).GetRoom("testroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFailure_WhenExceptionIsThrown()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom,Locale=fr-FR");
        _roomsManager.GetRoom("testroom").Returns(room);

        var paramDef = Substitute.For<IParameterDefinition>();
        paramDef.Identifier.Returns("Locale");
        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, paramDef }
        });
        room.SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new Exception("db error")));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("room_config_failure", "db error");
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleNoParameters_WhenTargetHasOnlyRoomId()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        await room.DidNotReceive().SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("room_config_success", "testroom");
    }
}
