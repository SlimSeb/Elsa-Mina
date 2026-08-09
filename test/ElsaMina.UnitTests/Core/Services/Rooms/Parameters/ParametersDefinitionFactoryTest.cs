using System.Globalization;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Rooms.Parameters;

public class ParametersDefinitionFactoryTest
{
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

        _factory = new ParametersDefinitionFactory(_configuration, _resourcesService);
    }

    [Test]
    public void Test_GetParametersDefinitions_ShouldContainEveryParameter()
    {
        // Act
        var definitions = _factory.GetParametersDefinitions();

        // Assert
        var expected = Enum.GetValues<Parameter>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(definitions, Has.Count.EqualTo(expected.Length));
            foreach (var parameter in expected)
            {
                Assert.That(definitions.ContainsKey(parameter), Is.True, $"Missing definition for {parameter}");
            }
        }
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
                Is.EquivalentTo(new[] { "en-US", "fr-FR" }));
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
    [TestCase(Parameter.HasCommandAutoCorrect, true)]
    [TestCase(Parameter.ShowErrorMessages, true)]
    [TestCase(Parameter.ShowTeamLinksPreview, true)]
    [TestCase(Parameter.ShowReplaysPreview, true)]
    [TestCase(Parameter.TournamentBettingEnabled, true)]
    [TestCase(Parameter.ShowYoutubeLinkPreview, true)]
    [TestCase(Parameter.KlipyGifEnabled, true)]
    [TestCase(Parameter.ShowUrlPreview, false)]
    [TestCase(Parameter.BucksEnabled, false)]
    public void Test_BooleanParameters_ShouldHaveExpectedDefault(Parameter parameter, bool expectedDefault)
    {
        // Act
        var definition = _factory.GetParametersDefinitions()[parameter];

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
        var definition = _factory.GetParametersDefinitions()[Parameter.EventAnnouncesType];

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
