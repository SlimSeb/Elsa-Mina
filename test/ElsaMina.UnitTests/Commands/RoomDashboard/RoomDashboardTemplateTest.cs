using System.Globalization;
using ElsaMina.Commands.Economy;
using ElsaMina.Commands.EventAnnounces;
using ElsaMina.Commands.Games.Catalog;
using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Commands.Misc.UrlPreview;
using ElsaMina.Commands.Misc.Youtube;
using ElsaMina.Commands.Replays;
using ElsaMina.Commands.RoomDashboard;
using ElsaMina.Commands.Teams.TeamPreviewOnLink;
using ElsaMina.Commands.Tournaments.Betting;
using ElsaMina.Commands.Users.Streaks;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Utils;
using ElsaMina.UnitTests.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.RoomDashboard;

/// <summary>
/// Renders the real dashboard templates with every room parameter, time zones included, as the bot sends them.
/// </summary>
public class RoomDashboardTemplateTest : TemplateTestBase
{
    // Showdown drops any message a client sends above 100KB
    private const int SHOWDOWN_MAX_MESSAGE_LENGTH = 100 * 1024;

    // More values than the template shows as buttons, so it renders a select
    private static readonly string[] SelectPossibleValues = ["a", "b", "c", "d", "e"];

    private List<RoomParameterLineModel> _parameterLines;

    [SetUp]
    public void SetUp()
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration.DefaultLocaleCode.Returns("en-US");
        var resourcesService = Substitute.For<IResourcesService>();
        resourcesService.SupportedCultures.Returns([new CultureInfo("en-US"), new CultureInfo("fr-FR")]);
        var factory = new ParametersDefinitionFactory(
        [
            new CoreRoomParameters(configuration, resourcesService),
            new TeamPreviewRoomParameters(),
            new ReplaysRoomParameters(),
            new TournamentBettingRoomParameters(),
            new YoutubeRoomParameters(),
            new UrlPreviewRoomParameters(),
            new KlipyRoomParameters(),
            new EconomyRoomParameters(),
            new EventAnnouncesRoomParameters(),
            new StreaksRoomParameters()
        ]);

        _parameterLines = factory.GetParametersDefinitions()
            .Select(pair => CreateLine(pair.Key, pair.Value, pair.Value.DefaultValue))
            .ToList();
    }

    [TestCase("RoomDashboard/RoomDashboard")]
    [TestCase("RoomDashboard/RoomOptions")]
    public async Task Test_Render_ShouldFitInOneShowdownMessage_WhenEveryParameterIsShown(string templateKey)
    {
        // Arrange
        var model = new RoomDashboardViewModel
        {
            Culture = Culture,
            BotName = "Bot",
            Trigger = "-",
            RoomId = "room",
            RoomName = "Room",
            RoomParameterLines = _parameterLines,
            AvailableGames = GamesCatalog.Games
        };

        // Act
        var html = await RenderAsync(templateKey, model);
        var sentHtml = html.RemoveNewlines().CollapseAttributeWhitespace().CollapseWhitespacesBetweenTags();

        // Assert
        Assert.That(sentHtml, Has.Length.LessThan(SHOWDOWN_MAX_MESSAGE_LENGTH));
    }

    [Test]
    public async Task Test_RenderParameterLine_ShouldSelectCurrentValue_WhenEnumerationIsShownAsSelect()
    {
        // Arrange
        var definition = new ParameterDefinition
        {
            Identifier = "tst",
            Name = "Test",
            NameKey = "name",
            DescriptionKey = "description",
            Type = RoomBotConfigurationType.Enumeration,
            DefaultValue = "a",
            PossibleValues = SelectPossibleValues
                .Select(value => new EnumerationValue { DisplayedValue = value.ToUpperInvariant(), InternalValue = value })
                .ToList()
        };
        var line = CreateLine(new Parameter("tst", "Test"), definition, "c");

        // Act
        var html = await RenderAsync("RoomDashboard/RoomParameterLine", line);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(html, Does.Contain("<option value=\"c\" selected>C</option>"));
            Assert.That(html, Does.Contain("<option value=\"a\">A</option>"));
            Assert.That(html, Does.Not.Contain("string.Equals"));
        }
    }

    private static RoomParameterLineModel CreateLine(Parameter parameter, IParameterDefinition definition,
        string currentValue) => new()
    {
        Culture = Culture,
        ParameterKey = parameter,
        RoomParameterDefinition = definition,
        CurrentValue = currentValue,
        BotName = "Bot",
        Trigger = "-",
        RoomId = "room"
    };
}
