using System.Globalization;
using ElsaMina.Commands.RoomDashboard;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.RoomDashboard;

[TestFixture]
public class ShowRoomDashboardTest
{
    private IContext _context;
    private IConfiguration _configuration;
    private IRoomsManager _roomsManager;
    private ITemplatesManager _templatesManager;
    private ShowRoomDashboard _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _configuration = Substitute.For<IConfiguration>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _command = new ShowRoomDashboard(_configuration, _roomsManager, _templatesManager);

        _configuration.Name.Returns("ElsaBot");
        _configuration.Trigger.Returns("-");
        _context.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));
        _roomsManager.ParametersDefinitions.Returns(new Dictionary<Parameter, IParameterDefinition>());
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("rendered-html");
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
    public async Task Test_RunAsync_ShouldReplyRoomDoesntExist_WhenRoomIsNotFound()
    {
        _context.Target.Returns("unknownroom");
        _roomsManager.GetRoom("unknownroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("dashboard_room_doesnt_exist", "unknownroom");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseFallbackRoomId_WhenTargetIsEmpty()
    {
        _context.Target.Returns("  ");
        _context.RoomId.Returns("defaultroom");
        _roomsManager.GetRoom("defaultroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        _roomsManager.Received(1).GetRoom("defaultroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTrimmedLowercaseTarget_WhenTargetIsProvided()
    {
        _context.Target.Returns("  TestRoom  ");
        _roomsManager.GetRoom("testroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        _roomsManager.Received(1).GetRoom("testroom");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetContextCultureToRoomCulture_WhenContextIsPrivateMessage()
    {
        var room = Substitute.For<IRoom>();
        var roomCulture = CultureInfo.GetCultureInfo("fr-FR");
        room.Name.Returns("Test Room");
        room.Culture.Returns(roomCulture);
        _context.Target.Returns("testroom");
        _context.IsPrivateMessage.Returns(true);
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        Assert.That(_context.Culture, Is.EqualTo(roomCulture));
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotSetContextCulture_WhenContextIsNotPrivateMessage()
    {
        var room = Substitute.For<IRoom>();
        var roomCulture = CultureInfo.GetCultureInfo("fr-FR");
        room.Name.Returns("Test Room");
        room.Culture.Returns(roomCulture);
        _context.Target.Returns("testroom");
        _context.IsPrivateMessage.Returns(false);
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        _context.DidNotReceiveWithAnyArgs().Culture = Arg.Any<CultureInfo>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallGetTemplateAsync_WhenRoomExists()
    {
        var room = Substitute.For<IRoom>();
        room.Name.Returns("Test Room");
        room.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));
        _context.Target.Returns("testroom");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync("RoomDashboard/RoomDashboard", Arg.Any<RoomDashboardViewModel>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallReplyHtmlPage_WhenRoomExists()
    {
        var room = Substitute.For<IRoom>();
        room.Name.Returns("Test Room");
        room.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));
        _context.Target.Returns("testroom");
        _roomsManager.GetRoom("testroom").Returns(room);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtmlPage("testroomdashboard", Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldBuildCommandWithCorrectFormat_WhenRoomExists()
    {
        var room = Substitute.For<IRoom>();
        room.Name.Returns("Test Room");
        room.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));
        _context.Target.Returns("testroom");
        _configuration.Name.Returns("MyBot");
        _configuration.Trigger.Returns("!");
        _roomsManager.GetRoom("testroom").Returns(room);

        RoomDashboardViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Do<RoomDashboardViewModel>(vm => capturedViewModel = vm));

        await _command.RunAsync(_context);

        Assert.That(capturedViewModel, Is.Not.Null);
        Assert.That(capturedViewModel.Command, Does.StartWith("/w MyBot,!rc testroom,"));
        Assert.That(capturedViewModel.BotName, Is.EqualTo("MyBot"));
        Assert.That(capturedViewModel.Trigger, Is.EqualTo("!"));
        Assert.That(capturedViewModel.RoomId, Is.EqualTo("testroom"));
        Assert.That(capturedViewModel.RoomName, Is.EqualTo("Test Room"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeParameterLinesInViewModel_WhenRoomHasParameters()
    {
        var room = Substitute.For<IRoom>();
        room.Name.Returns("Test Room");
        room.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));
        _context.Target.Returns("testroom");
        _roomsManager.GetRoom("testroom").Returns(room);

        var paramDef = Substitute.For<IParameterDefinition>();
        paramDef.Identifier.Returns("Locale");
        _roomsManager.ParametersDefinitions.Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, paramDef }
        });
        room.GetParameterValueAsync(Parameter.Locale, Arg.Any<CancellationToken>()).Returns("en-US");

        RoomDashboardViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Do<RoomDashboardViewModel>(vm => capturedViewModel = vm));

        await _command.RunAsync(_context);

        Assert.That(capturedViewModel, Is.Not.Null);
        Assert.That(capturedViewModel.RoomParameterLines, Has.Exactly(1).Items);
        var line = capturedViewModel.RoomParameterLines.First();
        Assert.That(line.RoomParameterDefinition, Is.EqualTo(paramDef));
        Assert.That(line.CurrentValue, Is.EqualTo("en-US"));
    }
}
