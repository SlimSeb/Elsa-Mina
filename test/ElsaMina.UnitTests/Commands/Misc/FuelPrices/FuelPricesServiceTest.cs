using ElsaMina.Commands.Misc.FuelPrices;
using ElsaMina.Core.Services.Http;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.FuelPrices;

public class FuelPricesServiceTest
{
    private static readonly string[] EXPECTED_LYON_CITIES = ["Lyon", "Chazelles-sur-Lyon"];

    private IHttpService _httpService;
    private FuelPricesService _service;
    private List<HttpRequest> _statisticsRequests;
    private List<HttpRequest> _cheapestStationRequests;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _service = new FuelPricesService(_httpService);
        _statisticsRequests = [];
        _cheapestStationRequests = [];

        _httpService.SendAsync<OpenDataRecordsResponse<FuelPriceStatisticsDto>>(
                Arg.Do<HttpRequest>(request => _statisticsRequests.Add(request)), Arg.Any<CancellationToken>())
            .Returns(Response(new OpenDataRecordsResponse<FuelPriceStatisticsDto> { Results = [] }));
        _httpService.SendAsync<OpenDataRecordsResponse<FuelStationDto>>(
                Arg.Do<HttpRequest>(request => _cheapestStationRequests.Add(request)), Arg.Any<CancellationToken>())
            .Returns(Response(new OpenDataRecordsResponse<FuelStationDto> { Results = [] }));
    }

    private static HttpResponse<T> Response<T>(T data)
    {
        return new HttpResponse<T> { Data = data };
    }

    private static bool IsRequestForField(HttpRequest request, string field)
    {
        return request.QueryParameters["where"].StartsWith(field)
               || request.QueryParameters["where"].Contains($" {field} ");
    }

    private void SetUpFuel(string priceField, double average, int stationCount, FuelStationDto cheapestStation)
    {
        _httpService.SendAsync<OpenDataRecordsResponse<FuelPriceStatisticsDto>>(
                Arg.Is<HttpRequest>(request => IsRequestForField(request, priceField)), Arg.Any<CancellationToken>())
            .Returns(Response(new OpenDataRecordsResponse<FuelPriceStatisticsDto>
            {
                Results = [new FuelPriceStatisticsDto { Average = average, StationCount = stationCount }]
            }));
        _httpService.SendAsync<OpenDataRecordsResponse<FuelStationDto>>(
                Arg.Is<HttpRequest>(request => IsRequestForField(request, priceField)), Arg.Any<CancellationToken>())
            .Returns(Response(new OpenDataRecordsResponse<FuelStationDto> { Results = [cheapestStation] }));
    }

    [Test]
    public async Task Test_GetSummariesAsync_ShouldReturnOneSummaryPerFuelWithPrices_WhenSomeFuelsHaveNoRecentPrice()
    {
        // Arrange
        var dieselStation = new FuelStationDto { Address = "1 RUE A", City = "Lyon", PostalCode = "69008", Price = 2.039 };
        var e10Station = new FuelStationDto { Address = "2 RUE B", City = "Lyon", PostalCode = "69007", Price = 1.973 };
        SetUpFuel("gazole_prix", 2.1, 20, dieselStation);
        SetUpFuel("e10_prix", 2.0, 17, e10Station);

        // Act
        var summaries = await _service.GetSummariesAsync(new FuelPriceArea(FuelPriceAreaKind.City, "Lyon"));

        // Assert
        Assert.That(summaries, Is.EqualTo(new[]
        {
            new FuelPriceSummary(new FuelType("gazole"), 2.1, 20, dieselStation),
            new FuelPriceSummary(new FuelType("e10"), 2.0, 17, e10Station)
        }));
    }

    [Test]
    public async Task Test_GetSummariesAsync_ShouldQueryEveryFuelTwice_WhenCalled()
    {
        // Act
        await _service.GetSummariesAsync(FuelPriceArea.Country);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_statisticsRequests, Has.Count.EqualTo(FuelType.All.Count));
            Assert.That(_cheapestStationRequests, Has.Count.EqualTo(FuelType.All.Count));
        }
    }

    [Test]
    public async Task Test_GetSummariesAsync_ShouldOnlyFilterOnRecentPrices_WhenAreaIsCountry()
    {
        // Act
        await _service.GetSummariesAsync(FuelPriceArea.Country);

        // Assert
        var dieselRequest = _statisticsRequests.Single(request => IsRequestForField(request, "gazole_prix"));
        Assert.That(dieselRequest.QueryParameters["where"],
            Is.EqualTo("gazole_prix is not null and gazole_maj >= now(days=-7)"));
    }

    [TestCase(FuelPriceAreaKind.Department, "2A", "code_departement = \"2A\"")]
    [TestCase(FuelPriceAreaKind.PostalCode, "75015", "cp = \"75015\"")]
    [TestCase(FuelPriceAreaKind.City, "L'Isle-Adam", "ville = \"L'Isle-Adam\"")]
    [TestCase(FuelPriceAreaKind.City, "a\"b\\c", "ville = \"a\\\"b\\\\c\"")]
    public async Task Test_GetSummariesAsync_ShouldFilterOnArea_WhenAreaIsGiven(FuelPriceAreaKind kind, string value,
        string expectedAreaFilter)
    {
        // Act
        await _service.GetSummariesAsync(new FuelPriceArea(kind, value));

        // Assert
        var dieselRequest = _cheapestStationRequests.Single(request => IsRequestForField(request, "gazole_prix"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dieselRequest.QueryParameters["where"],
                Is.EqualTo($"{expectedAreaFilter} and gazole_prix is not null and gazole_maj >= now(days=-7)"));
            Assert.That(dieselRequest.QueryParameters["order_by"], Is.EqualTo("gazole_prix asc"));
            Assert.That(dieselRequest.QueryParameters["limit"], Is.EqualTo("1"));
        }
    }

    [Test]
    public async Task Test_GetSummariesAsync_ShouldLeaveFuelOut_WhenStatisticsHaveNoAverage()
    {
        // Arrange
        _httpService.SendAsync<OpenDataRecordsResponse<FuelPriceStatisticsDto>>(Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Response(new OpenDataRecordsResponse<FuelPriceStatisticsDto>
            {
                Results = [new FuelPriceStatisticsDto { Average = null, StationCount = 0 }]
            }));
        _httpService.SendAsync<OpenDataRecordsResponse<FuelStationDto>>(Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Response(new OpenDataRecordsResponse<FuelStationDto> { Results = [new FuelStationDto()] }));

        // Act
        var summaries = await _service.GetSummariesAsync(FuelPriceArea.Country);

        // Assert
        Assert.That(summaries, Is.Empty);
    }

    [Test]
    public async Task Test_FindCitiesAsync_ShouldReturnCityNames_WhenCitiesMatch()
    {
        // Arrange
        HttpRequest sentRequest = null;
        _httpService.SendAsync<OpenDataRecordsResponse<FuelCityDto>>(Arg.Do<HttpRequest>(request => sentRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(Response(new OpenDataRecordsResponse<FuelCityDto>
            {
                Results = [new FuelCityDto { City = "Lyon" }, new FuelCityDto { City = "Chazelles-sur-Lyon" }]
            }));

        // Act
        var cities = await _service.FindCitiesAsync("lyon");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cities, Is.EqualTo(EXPECTED_LYON_CITIES));
            Assert.That(sentRequest.QueryParameters["where"], Is.EqualTo("ville like \"lyon\""));
            Assert.That(sentRequest.QueryParameters["group_by"], Is.EqualTo("ville as city"));
        }
    }

    [Test]
    public async Task Test_FindCitiesAsync_ShouldReturnEmptyList_WhenResponseHasNoData()
    {
        // Arrange
        _httpService.SendAsync<OpenDataRecordsResponse<FuelCityDto>>(Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Response<OpenDataRecordsResponse<FuelCityDto>>(null));

        // Act
        var cities = await _service.FindCitiesAsync("nowhere");

        // Assert
        Assert.That(cities, Is.Empty);
    }
}
