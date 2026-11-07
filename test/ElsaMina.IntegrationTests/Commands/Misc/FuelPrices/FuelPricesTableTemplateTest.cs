using System.Globalization;
using ElsaMina.Commands.Misc.FuelPrices;
using ElsaMina.Commands.Users.Colors;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Templates;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Misc.FuelPrices;

public class FuelPricesTableTemplateTest
{
    private TemplatesManager _templatesManager;

    [SetUp]
    public void SetUp()
    {
        var resourcesService = Substitute.For<IResourcesService>();
        resourcesService.GetString(Arg.Any<string>(), Arg.Any<CultureInfo>())
            .Returns(callInfo => $"[{callInfo.ArgAt<string>(0)}]");
        var serviceProvider = new ServiceCollection()
            .AddSingleton(resourcesService)
            .AddSingleton(Substitute.For<IUserColorsService>())
            .BuildServiceProvider();

        _templatesManager = new TemplatesManager(serviceProvider);
        _templatesManager.LoadTemplates();
    }

    private static FuelPricesTableViewModel CreateViewModel(string culture)
    {
        return new FuelPricesTableViewModel
        {
            Culture = new CultureInfo(culture),
            AreaName = "Lyon",
            RecentPriceDays = 7,
            Summaries =
            [
                new FuelPriceSummary(new FuelType("gazole"), 2.20051, 20, new FuelStationDto
                {
                    Address = "<b>86 AVENUE JEAN MERMOZ</b>",
                    City = "Lyon",
                    PostalCode = "69008",
                    Price = 1.973,
                    UpdatedAt = new DateTimeOffset(2026, 11, 7, 0, 1, 0, TimeSpan.Zero)
                })
            ]
        };
    }

    [Test]
    public async Task Test_GetTemplateAsync_ShouldRenderPricesInCulture_WhenCultureIsFrench()
    {
        // Act
        var html = await _templatesManager.GetTemplateAsync("Misc/FuelPrices/FuelPricesTable",
            CreateViewModel("fr-FR"));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(html, Does.Contain("[fuelprices_fuel_gazole]"));
            Assert.That(html, Does.Contain("2,201&nbsp;€"));
            Assert.That(html, Does.Contain("1,973&nbsp;€"));
            Assert.That(html, Does.Contain("69008 Lyon"));
            Assert.That(html, Does.Contain(">20<"));
        }
    }

    [Test]
    public async Task Test_GetTemplateAsync_ShouldRenderPricesWithDot_WhenCultureIsEnglish()
    {
        // Act
        var html = await _templatesManager.GetTemplateAsync("Misc/FuelPrices/FuelPricesTable",
            CreateViewModel("en-US"));

        // Assert
        Assert.That(html, Does.Contain("2.201&nbsp;€"));
    }

    [Test]
    public async Task Test_GetTemplateAsync_ShouldEncodeStationAddress_WhenAddressContainsHtml()
    {
        // Act
        var html = await _templatesManager.GetTemplateAsync("Misc/FuelPrices/FuelPricesTable",
            CreateViewModel("fr-FR"));

        // Assert
        Assert.That(html, Does.Not.Contain("<b>86 AVENUE"));
    }
}
