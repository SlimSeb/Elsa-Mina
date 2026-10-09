using System.Globalization;
using Autofac;
using ElsaMina.Cloud;
using ElsaMina.Cloud.GoogleDrive;
using ElsaMina.Cloud.Sheets;
using ElsaMina.Console.Startup;
using ElsaMina.Core;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Dispatch;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Startup;

/// <summary>
/// Construit le vrai container, comme ça une registration manquante ou circulaire pète ici et pas au démarrage.
/// On remplace juste l'infra qui taperait le réseau
/// </summary>
public class ContainerBootstrapperTest
{
    private IContainer _container;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var configuration = new Configuration
        {
            Host = "localhost",
            Port = "8000",
            Name = "Bot",
            Trigger = "-",
            DefaultLocaleCode = "en-US",
            Rooms = [],
            RoomBlacklist = [],
            Whitelist = [],
            ConnectionString = "Host=localhost;Database=elsamina",
            EventAnnounces = new Dictionary<string, IEnumerable<string>>(),
            PlayTimeUpdatesInterval = TimeSpan.FromMinutes(5),
            UserUpdateFlushInterval = TimeSpan.FromMinutes(5),
            UserUpdateBatchSize = 10
        };

        _container = ContainerBootstrapper.Build(configuration, builder =>
        {
            builder.RegisterInstance(Substitute.For<IClient>()).As<IClient>();
            builder.RegisterInstance(Substitute.For<IFileSharingService>()).As<IFileSharingService>();
            builder.RegisterInstance(Substitute.For<ISheetProvider>()).As<ISheetProvider>();
            builder.RegisterInstance(Substitute.For<IDriveProvider>()).As<IDriveProvider>();
        });
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _container.Dispose();
    }

    [Test]
    public void Test_Build_ShouldResolveRuntimeEntryPoints()
    {
        // Act & Assert
        Assert.That(_container.Resolve<IBot>(), Is.Not.Null);
        Assert.That(_container.Resolve<IIncomingMessageDispatcher>(), Is.Not.Null);
    }

    [Test]
    public void Test_Build_ShouldResolveEveryHandler()
    {
        // Arrange
        var handlerManager = _container.Resolve<IHandlerManager>();

        // Act
        handlerManager.Initialize();

        // Assert
        Assert.That(handlerManager.Handlers, Is.Not.Empty);
    }

    [Test]
    public void Test_Build_ShouldResolveEveryCommand_WithAName()
    {
        // Act
        var commands = _container.Resolve<ICommandRegistry>().Commands;

        // Assert
        Assert.That(commands, Is.Not.Empty);
        Assert.That(commands.Select(command => command.Name), Has.All.Not.Empty);
    }

    [Test]
    public void Test_Build_ShouldResolveEveryLifecycleParticipant()
    {
        // Act
        var participants = _container.Resolve<IEnumerable<IBotLifecycleParticipant>>().ToList();

        // Assert
        Assert.That(participants, Is.Not.Empty);
        Assert.That(participants.Select(participant => participant.GetType()), Is.Unique);
    }

    [Test]
    public void Test_Build_ShouldCollectEveryRoomParameter()
    {
        // Act
        var definitions = _container.Resolve<IParametersDefinitionFactory>().GetParametersDefinitions();

        // Assert
        Assert.That(definitions, Has.Count.EqualTo(13));
    }

    [Test]
    [TestCase("parameter_name_locale")]
    [TestCase("parameter_name_bucks_enabled")]
    [TestCase("parameter_name_event_announces_type")]
    [TestCase("parameter_value_event_announces_games")]
    public void Test_Build_ShouldFindRoomParameterStrings_InEveryLocale(string key)
    {
        // Arrange
        var resourcesService = _container.Resolve<IResourcesService>();

        // Act & Assert
        foreach (var culture in new[] { "en-US", "fr-FR", "es-ES", "it-IT", "pt-BR", "de-DE" })
        {
            var value = resourcesService.GetString(key, new CultureInfo(culture));
            Assert.That(value, Is.Not.Empty.And.Not.EqualTo(key), $"{key} is missing in {culture}");
        }
    }
}
