using ElsaMina.Core.Services.CustomColors;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Core.Services.Templates;

public class TemplatesTest
{
    private TemplatesManager _templatesManager;

    [SetUp]
    public void SetUp()
    {
        var userColorsService = Substitute.For<IUserColorsService>();
        userColorsService.GetUserColor(Arg.Any<string>()).Returns("#000000");
        var serviceProvider = new ServiceCollection()
            .AddSingleton(userColorsService)
            .BuildServiceProvider();

        _templatesManager = new TemplatesManager(serviceProvider);
        _templatesManager.LoadTemplates();
    }

    [Test]
    public async Task Test_GetTemplateAsync_ShouldRenderTemplate_WhenKeyMatchesTemplatePath()
    {
        var badge = new Badge { Name = "Champion", Image = " https://example.com/badge.png " };

        var html = await _templatesManager.GetTemplateAsync("Badges/Badge", badge);

        Assert.That(html, Does.Contain("src=\"https://example.com/badge.png\""));
        Assert.That(html, Does.Contain("title=\"Champion\""));
    }

    [Test]
    public async Task Test_GetTemplateAsync_ShouldEncodeModelValues()
    {
        var badge = new Badge { Name = "<script>", Image = "https://example.com/badge.png" };

        var html = await _templatesManager.GetTemplateAsync("Badges/Badge", badge);

        Assert.That(html, Does.Not.Contain("<script>"));
    }

    [Test]
    public async Task Test_GetTemplateAsync_ShouldReturnNull_WhenTemplateDoesNotExist()
    {
        var html = await _templatesManager.GetTemplateAsync("Unknown/Template", new object());

        Assert.That(html, Is.Null);
    }
}
