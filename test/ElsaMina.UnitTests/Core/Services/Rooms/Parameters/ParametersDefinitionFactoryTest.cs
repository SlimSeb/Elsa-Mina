using System.Globalization;
using ElsaMina.Commands.Economy;
using ElsaMina.Commands.EventAnnounces;
using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Commands.Misc.UrlPreview;
using ElsaMina.Commands.Misc.Youtube;
using ElsaMina.Commands.Replays;
using ElsaMina.Commands.Teams.TeamPreviewOnLink;
using ElsaMina.Commands.Tournaments.Betting;
using ElsaMina.Commands.Users.Streaks;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Rooms.Parameters;

public class ParametersDefinitionFactoryTest
{
    private static readonly string[] ExpectedLocalePossibleValues = ["en-US", "fr-FR"];

    private IConfiguration _configuration;
    private IResourcesService _resourcesService;
    private ParametersDefinitionFactory _factory;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _configuration.DefaultLocaleCode.Returns("en-US");

        _resourcesService = Substitute.For<IResourcesService>();
        _resourcesService.SupportedCultures.Returns(new[]
        {
            new CultureInfo("en-US"),
            new CultureInfo("fr-FR")
        });

        _factory = new ParametersDefinitionFactory(
        [
            new CoreRoomParameters(_configuration, _resourcesService),
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
    }

    [Test]
    public void Test_GetParametersDefinitions_ShouldKeepStoredIdentifiers()
    {
        // Les valeurs sont stockées en bdd sous ces identifiants : si on en change un, toutes les rooms perdent leur réglage
        string[] expectedIdentifiers =
            ["loc", "tzn", "atc", "err", "tms", "rpl", "tbe", "ytl", "urlp", "tgf", "bck", "evn", "stk"];

        // Act
        var definitions = _factory.GetParametersDefinitions();

        // Assert
        Assert.That(definitions.Keys.Select(parameter => parameter.Identifier), Is.EquivalentTo(expectedIdentifiers));
    }

    [Test]
    public void Test_GetParametersDefinitions_ShouldListCoreParametersFirst()
    {
        // Act
        var firstParameters = _factory.GetParametersDefinitions().Keys.Take(4);

        // Assert
        Assert.That(firstParameters, Is.EqualTo(new[]
        {
            Parameter.Locale, Parameter.TimeZone, Parameter.HasCommandAutoCorrect, Parameter.ShowErrorMessages
        }));
    }

    [Test]
    public void Test_GetParametersDefinitions_ShouldThrow_WhenTwoProvidersShareAnIdentifier()
    {
        // Arrange
        var factory = new ParametersDefinitionFactory([new EconomyRoomParameters(), new EconomyRoomParameters()]);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => factory.GetParametersDefinitions());
    }

    [Test]
    public void Test_GetParametersDefinitions_ShouldHaveUniqueIdentifiers()
    {
        // Act
        var identifiers = _factory.GetParametersDefinitions().Values
            .Select(definition => definition.Identifier)
            .ToList();

        // Assert
        Assert.That(identifiers, Is.Unique);
    }

    [Test]
    public void Test_LocaleDefinition_ShouldBeEnumerationWithConfiguredDefault()
    {
        // Act
        var locale = _factory.GetParametersDefinitions()[Parameter.Locale];

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(locale.Identifier, Is.EqualTo("loc"));
            Assert.That(locale.Type, Is.EqualTo(RoomBotConfigurationType.Enumeration));
            Assert.That(locale.DefaultValue, Is.EqualTo("en-US"));
            Assert.That(locale.PossibleValues.Select(value => value.InternalValue),
                Is.EquivalentTo(ExpectedLocalePossibleValues));
        }
    }

    [Test]
    public void Test_LocaleOnUpdateAction_ShouldSetRoomCulture()
    {
        // Arrange
        var room = Substitute.For<IRoom>();
        var locale = _factory.GetParametersDefinitions()[Parameter.Locale];

        // Act
        locale.OnUpdateAction(room, "fr-FR");

        // Assert
        room.Received().Culture = Arg.Is<CultureInfo>(culture => culture.Name == "fr-FR");
    }

    [Test]
    public void Test_TimeZoneOnUpdateAction_ShouldSetRoomTimeZone()
    {
        // Arrange
        var room = Substitute.For<IRoom>();
        var timeZone = _factory.GetParametersDefinitions()[Parameter.TimeZone];
        var localZoneId = TimeZoneInfo.Local.Id;

        // Act
        timeZone.OnUpdateAction(room, localZoneId);

        // Assert
        room.Received().TimeZone = Arg.Is<TimeZoneInfo>(zone => zone.Id == localZoneId);
    }

    [Test]
    [TestCase("atc", true)]
    [TestCase("err", true)]
    [TestCase("tms", true)]
    [TestCase("rpl", true)]
    [TestCase("tbe", true)]
    [TestCase("ytl", true)]
    [TestCase("tgf", true)]
    [TestCase("urlp", false)]
    [TestCase("bck", false)]
    [TestCase("stk", true)]
    public void Test_BooleanParameters_ShouldHaveExpectedDefault(string identifier, bool expectedDefault)
    {
        // Act
        var definition = _factory.GetParametersDefinitions()[new Parameter(identifier)];

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(definition.Type, Is.EqualTo(RoomBotConfigurationType.Boolean));
            Assert.That(definition.DefaultValue, Is.EqualTo(expectedDefault.ToString()));
        }
    }

    [Test]
    public void Test_EventAnnouncesTypeDefinition_ShouldBeEnumerationDefaultingToTournamentsWithEveryOption()
    {
        // Act
        var definition = _factory.GetParametersDefinitions()[EventAnnouncesRoomParameters.EventAnnouncesType];

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(definition.Identifier, Is.EqualTo("evn"));
            Assert.That(definition.Type, Is.EqualTo(RoomBotConfigurationType.Enumeration));
            Assert.That(definition.DefaultValue, Is.EqualTo(EventAnnouncesTypeValues.TournamentsOnly));
            Assert.That(definition.PossibleValues.Select(value => value.InternalValue),
                Is.EquivalentTo(new[]
                {
                    EventAnnouncesTypeValues.All,
                    EventAnnouncesTypeValues.TournamentsOnly,
                    EventAnnouncesTypeValues.GamesOnly,
                    EventAnnouncesTypeValues.None
                }));
        }
    }

    [Test]
    public void Test_GetParametersDefinitions_ShouldReturnCachedInstance()
    {
        // Act
        var first = _factory.GetParametersDefinitions();
        var second = _factory.GetParametersDefinitions();

        // Assert
        Assert.That(first, Is.SameAs(second));
    }
}
