using System.Globalization;
using ElsaMina.Commands.Misc.FuelPrices;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.FuelPrices;

public class FuelPricesCommandTest
{
    private static readonly string[] SAINT_ETIENNE_CANDIDATES =
        ["Saint-Étienne-au-Mont", "Saint-Étienne", "Saint-Étienne-du-Rouvray"];

    private static readonly string[] LYON_CANDIDATES = ["Chazelles-sur-Lyon", "Sainte-Foy-lès-Lyon"];

    private IFuelPricesService _fuelPricesService;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private FuelPricesCommand _command;
    private FuelPriceSummary _summary;

    [SetUp]
    public void SetUp()
    {
        _fuelPricesService = Substitute.For<IFuelPricesService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _context = Substitute.For<IContext>();
        _context.Culture.Returns(new CultureInfo("fr-FR"));
        _context.GetString("fuelprices_area_country").Returns("France");
        _context.GetString("fuelprices_area_department", Arg.Any<object[]>())
            .Returns(callInfo => $"département {callInfo.ArgAt<object[]>(1)[0]}");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<table>\n</table>");

        _summary = new FuelPriceSummary(new FuelType("gazole"), 2.2, 10, new FuelStationDto { Price = 2.0 });
        _fuelPricesService.GetSummariesAsync(Arg.Any<FuelPriceArea>(), Arg.Any<CancellationToken>())
            .Returns([_summary]);

        _command = new FuelPricesCommand(_fuelPricesService, _templatesManager);
    }

    private async Task AssertAreaRequestedAsync(FuelPriceArea expectedArea)
    {
        await _fuelPricesService.Received(1).GetSummariesAsync(expectedArea, Arg.Any<CancellationToken>());
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task Test_RunAsync_ShouldRequestCountryPrices_WhenTargetIsEmpty(string target)
    {
        // Arrange
        _context.Target.Returns(target);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await AssertAreaRequestedAsync(FuelPriceArea.Country);
        await _templatesManager.Received(1).GetTemplateAsync("Misc/FuelPrices/FuelPricesTable",
            Arg.Is<FuelPricesTableViewModel>(viewModel => viewModel.AreaName == "France"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldRequestPostalCodePrices_WhenTargetIsFiveDigits()
    {
        // Arrange
        _context.Target.Returns(" 75015 ");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await AssertAreaRequestedAsync(new FuelPriceArea(FuelPriceAreaKind.PostalCode, "75015"));
        await _fuelPricesService.DidNotReceiveWithAnyArgs().FindCitiesAsync(default);
    }

    [TestCase("69", "69")]
    [TestCase("1", "01")]
    [TestCase("2a", "2A")]
    [TestCase("2B", "2B")]
    public async Task Test_RunAsync_ShouldRequestDepartmentPrices_WhenTargetIsDepartmentCode(string target,
        string expectedCode)
    {
        // Arrange
        _context.Target.Returns(target);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await AssertAreaRequestedAsync(new FuelPriceArea(FuelPriceAreaKind.Department, expectedCode));
        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(),
            Arg.Is<FuelPricesTableViewModel>(viewModel => viewModel.AreaName == $"département {expectedCode}"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldPickExactCity_WhenSeveralCitiesMatchIgnoringAccentsAndDashes()
    {
        // Arrange
        _context.Target.Returns("SAINT etienne");
        _fuelPricesService.FindCitiesAsync("saint etienne", Arg.Any<CancellationToken>())
            .Returns(SAINT_ETIENNE_CANDIDATES);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await AssertAreaRequestedAsync(new FuelPriceArea(FuelPriceAreaKind.City, "Saint-Étienne"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldPickOnlyCity_WhenOneCityMatches()
    {
        // Arrange
        _context.Target.Returns("isle adam");
        _fuelPricesService.FindCitiesAsync("isle adam", Arg.Any<CancellationToken>()).Returns(["L'Isle-Adam"]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await AssertAreaRequestedAsync(new FuelPriceArea(FuelPriceAreaKind.City, "L'Isle-Adam"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldListCandidates_WhenSeveralCitiesMatchButNoneExactly()
    {
        // Arrange
        _context.Target.Returns("lyo*n\"");
        _fuelPricesService.FindCitiesAsync("lyo n", Arg.Any<CancellationToken>()).Returns(LYON_CANDIDATES);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("fuelprices_ambiguous_city", "lyo*n\"",
            "Chazelles-sur-Lyon, Sainte-Foy-lès-Lyon");
        await _fuelPricesService.DidNotReceiveWithAnyArgs().GetSummariesAsync(default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyCityNotFound_WhenNoCityMatches()
    {
        // Arrange
        _context.Target.Returns("Atlantis");
        _fuelPricesService.FindCitiesAsync("atlantis", Arg.Any<CancellationToken>()).Returns([]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("fuelprices_city_not_found", "Atlantis");
        await _fuelPricesService.DidNotReceiveWithAnyArgs().GetSummariesAsync(default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyCityNotFound_WhenTargetHasNoLetterOrDigit()
    {
        // Arrange
        _context.Target.Returns("***");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("fuelprices_city_not_found", "***");
        await _fuelPricesService.DidNotReceiveWithAnyArgs().FindCitiesAsync(default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoPrices_WhenAreaHasNoRecentPrice()
    {
        // Arrange
        _context.Target.Returns("99");
        _fuelPricesService.GetSummariesAsync(Arg.Any<FuelPriceArea>(), Arg.Any<CancellationToken>()).Returns([]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("fuelprices_no_prices", "99");
        _context.DidNotReceiveWithAnyArgs().ReplyHtml(default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRenderedTable_WhenPricesAreFound()
    {
        // Arrange
        _context.Target.Returns("75015");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Misc/FuelPrices/FuelPricesTable",
            Arg.Is<FuelPricesTableViewModel>(viewModel =>
                viewModel.AreaName == "75015"
                && viewModel.Summaries.Single() == _summary
                && viewModel.RecentPriceDays == FuelPricesService.RECENT_PRICE_DAYS
                && viewModel.Culture.Name == "fr-FR"));
        _context.Received(1).ReplyHtml("<table></table>", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenServiceFails()
    {
        // Arrange
        _context.Target.Returns("75015");
        _fuelPricesService.GetSummariesAsync(Arg.Any<FuelPriceArea>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("down"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("fuelprices_error");
        _context.DidNotReceiveWithAnyArgs().ReplyHtml(default);
    }
}
