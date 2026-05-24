using System.Globalization;
using ElsaMina.Commands.Teams;
using ElsaMina.Commands.Teams.TeamPreviewOnLink;
using ElsaMina.Commands.Teams.TeamProviders;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Teams.TeamPreviewOnLink;

public class DisplayTeamOnLinkHandlerTest
{
    private DisplayTeamOnLinkHandler _handler;
    private IClockService _clockService;
    private ITeamLinkMatchFactory _teamLinkMatchFactory;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContextFactory _contextFactory;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _clockService = Substitute.For<IClockService>();
        _teamLinkMatchFactory = Substitute.For<ITeamLinkMatchFactory>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _contextFactory = Substitute.For<IContextFactory>();

        _context = Substitute.For<IContext>();
        _contextFactory.TryBuildContextFromReceivedMessage(Arg.Any<string[]>(), Arg.Any<string>())
            .Returns(_context);

        // Room mock
        _context.Room.GetParameterValueAsync(Parameter.ShowTeamLinksPreview, Arg.Any<CancellationToken>())
            .Returns("true"); // default behavior

        _handler = new DisplayTeamOnLinkHandler(
            _contextFactory,
            _clockService,
            _teamLinkMatchFactory,
            _templatesManager,
            _configuration
        );
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotProceed_WhenMessageStartsWithTrigger()
    {
        // Arrange
        _configuration.Trigger.Returns("!");
        _context.Message.Returns("!triggered");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _teamLinkMatchFactory.DidNotReceive().FindTeamLinkMatch(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotProceed_WhenSenderIsBot()
    {
        // Arrange
        _configuration.Name.Returns("BotName");
        _context.Sender.UserId.Returns("botname");
        _context.Message.Returns("some message");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _teamLinkMatchFactory.DidNotReceive().FindTeamLinkMatch(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotProceed_WhenTeamLinksPreviewDisabled()
    {
        // Arrange
        _context.Room.GetParameterValueAsync(Parameter.ShowTeamLinksPreview, Arg.Any<CancellationToken>())
            .Returns("false");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _teamLinkMatchFactory.DidNotReceive().FindTeamLinkMatch(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldThrottleUser_WhenCalledTooSoon()
    {
        // Arrange
        _configuration.Trigger.Returns("-");
        _clockService.CurrentUtcDateTime.Returns(DateTime.UtcNow);
        _context.Sender.UserId.Returns("user1");
        _context.IsSenderWhitelisted.Returns(false);

        // first call records the timestamp
        await _handler.HandleMessageAsync(_context);

        // Act - call again too soon
        await _handler.HandleMessageAsync(_context);

        // Assert
        _teamLinkMatchFactory.Received(1).FindTeamLinkMatch(Arg.Any<string>()); // only first call
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotProceed_WhenTeamLinkNotMatched()
    {
        // Arrange
        _context.Message.Returns("some message");
        _teamLinkMatchFactory.FindTeamLinkMatch("some message").Returns((ITeamLinkMatch)null);

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.DidNotReceive().GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Any<TeamPreviewViewModel>()
        );
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldSendHtml_WhenTeamLinkMatched()
    {
        // Arrange
        _configuration.Trigger.Returns("-");
        _configuration.Name.Returns("Bot");
        _context.Message.Returns("team link");
        _context.Culture.Returns(new CultureInfo("en-US"));
        _context.Sender.UserId.Returns("userId");
        _context.Sender.Name.Returns("User");
        _context.Room.GetParameterValueAsync(Parameter.ShowTeamLinksPreview, Arg.Any<CancellationToken>())
            .Returns("true");

        var teamLinkMatch = Substitute.For<ITeamLinkMatch>();
        var sharedTeam = new SharedTeam { Author = "Author", TeamExport = "Export" };
        teamLinkMatch.GetTeamExport().Returns(Task.FromResult(sharedTeam));

        _teamLinkMatchFactory.FindTeamLinkMatch("team link").Returns(teamLinkMatch);

        var expectedHtml = "<div>Team Preview HTML</div>";
        _templatesManager.GetTemplateAsync("Teams/TeamPreview", Arg.Any<TeamPreviewViewModel>())
            .Returns(Task.FromResult(expectedHtml));

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Teams/TeamPreview",
            Arg.Is<TeamPreviewViewModel>(vm =>
                vm.Author == "Author" &&
                vm.Culture.Name == "en-US" &&
                vm.Sender == "User" &&
                vm.Team != null
            )
        );

        _context.Received().ReplyHtml(expectedHtml.RemoveNewlines());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotSendHtml_WhenTeamIsEmpty()
    {
        // Arrange
        _configuration.Trigger.Returns("-");
        _configuration.Name.Returns("Bot");
        _context.Message.Returns("team link");

        var teamLinkMatch = Substitute.For<ITeamLinkMatch>();
        var sharedTeam = new SharedTeam { Author = "Author", TeamExport = "" };
        teamLinkMatch.GetTeamExport().Returns(Task.FromResult(sharedTeam));
        _teamLinkMatchFactory.FindTeamLinkMatch("team link").Returns(teamLinkMatch);

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<TeamPreviewViewModel>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotSendHtml_WhenTeamExceedsMaxSize()
    {
        // Arrange
        _configuration.Trigger.Returns("-");
        _configuration.Name.Returns("Bot");
        _context.Message.Returns("team link");

        const string sevenPokemonExport =
            "Bulbasaur\n\nCharmander\n\nSquirtle\n\nPikachu\n\nMewtwo\n\nGengar\n\nEevee";
        var teamLinkMatch = Substitute.For<ITeamLinkMatch>();
        var sharedTeam = new SharedTeam { Author = "Author", TeamExport = sevenPokemonExport };
        teamLinkMatch.GetTeamExport().Returns(Task.FromResult(sharedTeam));
        _teamLinkMatchFactory.FindTeamLinkMatch("team link").Returns(teamLinkMatch);

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<TeamPreviewViewModel>());
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldNotSendHtml_WhenTeamHasSetWithEmptySpecies()
    {
        // Arrange
        _configuration.Trigger.Returns("-");
        _configuration.Name.Returns("Bot");
        _context.Message.Returns("team link");

        // "Nickname ()" parses to a set with Species = ""
        var teamLinkMatch = Substitute.For<ITeamLinkMatch>();
        var sharedTeam = new SharedTeam { Author = "Author", TeamExport = "Nickname ()" };
        teamLinkMatch.GetTeamExport().Returns(Task.FromResult(sharedTeam));
        _teamLinkMatchFactory.FindTeamLinkMatch("team link").Returns(teamLinkMatch);

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<TeamPreviewViewModel>());
    }
}
