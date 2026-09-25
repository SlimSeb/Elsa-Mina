using System.Globalization;
using ElsaMina.Commands.Misc.Help;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Resources;
using NSubstitute;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Misc.Help;

[TestFixture]
public class HelpTemplateTest
{
    private TemplatesManager _templatesManager;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var resourcesService = Substitute.For<IResourcesService>();
        resourcesService.GetString(Arg.Any<string>(), Arg.Any<CultureInfo>())
            .Returns(callInfo =>
            {
                var key = callInfo.ArgAt<string>(0);
                return key == "help_bot_description" ? "description {0}" : key;
            });
        var containerService = Substitute.For<IDependencyContainerService>();
        containerService.Resolve<IResourcesService>().Returns(resourcesService);
        DependencyContainerService.Current = containerService;

        _templatesManager = new TemplatesManager();
        _templatesManager.LoadTemplates();
    }

    [Test]
    public async Task Test_Render_ShouldIncludeBugReportButton_WithSendCommand()
    {
        var model = new HelpViewModel
        {
            Culture = new CultureInfo("en-US"),
            BotName = "TestBot",
            Trigger = "-",
            Version = "1.0.0",
            RepositoryLink = "https://github.com/SlimSeb/Elsa-Mina"
        };

        var html = await _templatesManager.GetTemplateAsync("Misc/Help/Help", model);

        Assert.That(html, Does.Contain("<button name=\"send\" value=\"/w TestBot,-bugreport\">help_report_bug</button>"));
        Assert.That(html, Does.Contain("<button name=\"send\" value=\"/w TestBot,-allcommands\">help_commands_list</button>"));
        Assert.That(html, Does.Contain("<a href=\"https://github.com/SlimSeb/Elsa-Mina\" target=\"_blank\" rel=\"noopener\">"));
        Assert.That(html, Does.Not.Contain("<a href=\"\" target=\"_blank\" rel=\"noopener\">"));
    }
}
