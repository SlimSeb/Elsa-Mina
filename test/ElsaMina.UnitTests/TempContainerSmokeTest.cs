using Autofac;
using ElsaMina.Battles;
using ElsaMina.Commands;
using ElsaMina.Core;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Modules;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Cloud.S3;
using ElsaMina.Commands.Tournaments.Betting;
using NSubstitute;

namespace ElsaMina.UnitTests;

public class TempContainerSmokeTest
{
    private IContainer _container;

    [OneTimeSetUp]
    public void SetUp()
    {
        var configuration = Substitute.For<IConfiguration, IS3CredentialsProvider>();
        configuration.DefaultLocaleCode.Returns("en-US");
        configuration.ConnectionString.Returns("Host=localhost;Database=x;Username=x;Password=x");

        var builder = new ContainerBuilder();
        builder.RegisterInstance(configuration).As<IConfiguration>().As<IS3CredentialsProvider>().SingleInstance();
        builder.RegisterModule<CoreModule>();
        builder.RegisterModule<BattlesModule>();
        builder.RegisterModule<CommandModule>();
        _container = builder.Build();
    }

    [OneTimeTearDown]
    public void TearDown() => _container?.Dispose();

    [Test]
    public void Test_Container_ShouldResolveTheNewlyIntroducedServices()
    {
        Assert.DoesNotThrow(() => _container.Resolve<IBotLifecycleService>());
        Assert.DoesNotThrow(() => _container.Resolve<IBetRecordsStore>());
        Assert.DoesNotThrow(() => _container.Resolve<ITournamentBettingService>());
        Assert.DoesNotThrow(() => _container.Resolve<IBot>());
    }

    [Test]
    public void Test_Container_ShouldResolveEveryHandlerAndCommand()
    {
        var handlers = _container.Resolve<IEnumerable<IHandler>>().ToList();
        var commands = _container.Resolve<IEnumerable<ICommand>>().ToList();
        TestContext.Out.WriteLine($"handlers: {handlers.Count}, commands: {commands.Count}");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(handlers, Is.Not.Empty);
            Assert.That(commands, Is.Not.Empty);
        }
    }
}
