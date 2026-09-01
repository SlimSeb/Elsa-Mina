using System.Globalization;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.RoomDashboard;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.RoomDashboard;

[TestFixture]
public class RoomDashboardServiceTest
{
    private IConfiguration _configuration;
    private IRoomsManager _roomsManager;
    private ITemplatesManager _templatesManager;
    private IParametersDefinitionFactory _parametersDefinitionFactory;
    private IArcadeEventsService _arcadeEventsService;
    private IContext _context;
    private RoomDashboardService _service;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _parametersDefinitionFactory = Substitute.For<IParametersDefinitionFactory>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("ElsaBot");
        _configuration.Trigger.Returns("-");
        _context.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));

        _service = new RoomDashboardService(
            _configuration,
            _roomsManager,
            _templatesManager,
            _parametersDefinitionFactory,
            _arcadeEventsService);
    }

    [Test]
    public async Task Test_BuildViewModelAsync_ShouldReturnNull_WhenRoomDoesNotExist()
    {
        _roomsManager.GetRoom("unknown").Returns((IRoom)null);

        var result = await _service.BuildViewModelAsync("unknown", _context);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_BuildViewModelAsync_ShouldPopulateViewModel_WhenRoomExists()
    {
        var room = Substitute.For<IRoom>();
        room.Name.Returns("Test Room");
        _roomsManager.GetRoom("testroom").Returns(room);

        var localeDef = Substitute.For<IParameterDefinition>();
        localeDef.Identifier.Returns("loc");
        localeDef.Type.Returns(RoomBotConfigurationType.Enumeration);
        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, localeDef }
        });
        room.GetParameterValueAsync(Parameter.Locale, Arg.Any<CancellationToken>()).Returns("en-US");
        _arcadeEventsService.AreGamesMuted("testroom").Returns(false);

        var viewModel = await _service.BuildViewModelAsync("testroom", _context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel, Is.Not.Null);
            Assert.That(viewModel.BotName, Is.EqualTo("ElsaBot"));
            Assert.That(viewModel.Trigger, Is.EqualTo("-"));
            Assert.That(viewModel.RoomId, Is.EqualTo("testroom"));
            Assert.That(viewModel.RoomName, Is.EqualTo("Test Room"));
            Assert.That(viewModel.AreGamesMuted, Is.False);
            Assert.That(viewModel.Command, Does.Contain("/w ElsaBot, -rc testroom, loc={loc}"));
            Assert.That(viewModel.RoomParameterLines.Count(), Is.EqualTo(1));
            Assert.That(viewModel.Categories.Count(), Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Test_BuildViewModelAsync_ShouldGroupParametersIntoCorrectCategories()
    {
        var room = Substitute.For<IRoom>();
        room.Name.Returns("Test Room");
        _roomsManager.GetRoom("testroom").Returns(room);

        var localeDef = Substitute.For<IParameterDefinition>();
        localeDef.Identifier.Returns("loc");
        var teamLinksDef = Substitute.For<IParameterDefinition>();
        teamLinksDef.Identifier.Returns("tms");
        var bucksDef = Substitute.For<IParameterDefinition>();
        bucksDef.Identifier.Returns("bck");

        _parametersDefinitionFactory.GetParametersDefinitions().Returns(new Dictionary<Parameter, IParameterDefinition>
        {
            { Parameter.Locale, localeDef },
            { Parameter.ShowTeamLinksPreview, teamLinksDef },
            { Parameter.BucksEnabled, bucksDef }
        });
        room.GetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<CancellationToken>()).Returns("true");

        var viewModel = await _service.BuildViewModelAsync("testroom", _context);

        var categories = viewModel.Categories.ToList();
        var generalCategory = categories.FirstOrDefault(c => c.CategoryKey == "general");
        var previewCategory = categories.FirstOrDefault(c => c.CategoryKey == "previews");
        var arcadeCategory = categories.FirstOrDefault(c => c.CategoryKey == "arcade");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generalCategory, Is.Not.Null);
            Assert.That(generalCategory.Parameters.Any(p => p.ParameterKey == Parameter.Locale), Is.True);

            Assert.That(previewCategory, Is.Not.Null);
            Assert.That(previewCategory.Parameters.Any(p => p.ParameterKey == Parameter.ShowTeamLinksPreview), Is.True);

            Assert.That(arcadeCategory, Is.Not.Null);
            Assert.That(arcadeCategory.Parameters.Any(p => p.ParameterKey == Parameter.BucksEnabled), Is.True);
        }
    }

    [Test]
    public async Task Test_SendDashboardPageAsync_ShouldRenderTemplateAndReplyHtmlPage_WhenRoomExists()
    {
        var room = Substitute.For<IRoom>();
        room.Name.Returns("Test Room");
        _roomsManager.GetRoom("testroom").Returns(room);
        _parametersDefinitionFactory.GetParametersDefinitions()
            .Returns(new Dictionary<Parameter, IParameterDefinition>());
        _templatesManager.GetTemplateAsync("RoomDashboard/RoomDashboard", Arg.Any<RoomDashboardViewModel>())
            .Returns("<div>dashboard</div>\n");

        await _service.SendDashboardPageAsync(_context, "testroom");

        _context.Received(1).ReplyHtmlPage("testroomdashboard", "<div>dashboard</div>");
    }

    [Test]
    public async Task Test_SendDashboardPageAsync_ShouldDoNothing_WhenRoomNotFound()
    {
        _roomsManager.GetRoom("unknown").Returns((IRoom)null);

        await _service.SendDashboardPageAsync(_context, "unknown");

        await _templatesManager.DidNotReceive()
            .GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
        _context.DidNotReceive().ReplyHtmlPage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_SendOptionsPageAsync_ShouldRenderOptionsTemplateAndReplyHtmlPage_WhenRoomExists()
    {
        var room = Substitute.For<IRoom>();
        room.Name.Returns("Test Room");
        _roomsManager.GetRoom("testroom").Returns(room);
        _parametersDefinitionFactory.GetParametersDefinitions()
            .Returns(new Dictionary<Parameter, IParameterDefinition>());
        _templatesManager.GetTemplateAsync("RoomDashboard/RoomOptions", Arg.Any<RoomDashboardViewModel>())
            .Returns("<div>options</div>\n");

        await _service.SendOptionsPageAsync(_context, "testroom");

        _context.Received(1).ReplyHtmlPage("testroomdashboard", "<div>options</div>");
    }

    [Test]
    public async Task Test_SendOptionsPageAsync_ShouldDoNothing_WhenRoomNotFound()
    {
        _roomsManager.GetRoom("unknown").Returns((IRoom)null);

        await _service.SendOptionsPageAsync(_context, "unknown");

        await _templatesManager.DidNotReceive()
            .GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
        _context.DidNotReceive().ReplyHtmlPage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_BuildViewModelAsync_ShouldPopulateDiagnosticsAndGames_WhenRoomExists()
    {
        var room = Substitute.For<IRoom>();
        room.Name.Returns("Test Room");
        room.Users.Returns(new Dictionary<string, IUser>
        {
            { "user1", Substitute.For<IUser>() },
            { "user2", Substitute.For<IUser>() }
        });
        room.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));
        room.TimeZone.Returns(TimeZoneInfo.Utc);
        room.Game.Returns((IGame)null);
        _roomsManager.GetRoom("testroom").Returns(room);
        _parametersDefinitionFactory.GetParametersDefinitions()
            .Returns(new Dictionary<Parameter, IParameterDefinition>());

        var viewModel = await _service.BuildViewModelAsync("testroom", _context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.UserCount, Is.EqualTo(2));
            Assert.That(viewModel.AvailableGames, Is.Not.Null);
            Assert.That(viewModel.AvailableGames.Count, Is.GreaterThan(0));
            Assert.That(viewModel.RoomLocale, Does.Contain("English"));
            Assert.That(viewModel.RoomTimeZone, Is.Not.Null);
            Assert.That(viewModel.HasActiveGame, Is.False);
            Assert.That(viewModel.ActiveGameName, Is.Null);
        }
    }

    [Test]
    public async Task Test_BuildViewModelAsync_ShouldDetectActiveGame_WhenGameIsNotNull()
    {
        var room = Substitute.For<IRoom>();
        var mockGame = Substitute.For<IGame>();
        room.Name.Returns("Test Room");
        room.Game.Returns(mockGame);
        _roomsManager.GetRoom("testroom").Returns(room);
        _parametersDefinitionFactory.GetParametersDefinitions()
            .Returns(new Dictionary<Parameter, IParameterDefinition>());

        var viewModel = await _service.BuildViewModelAsync("testroom", _context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.HasActiveGame, Is.True);
            Assert.That(viewModel.ActiveGameName, Is.Not.Null);
        }
    }
}
