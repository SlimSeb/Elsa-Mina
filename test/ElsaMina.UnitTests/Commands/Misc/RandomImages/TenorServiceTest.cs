using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Probabilities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.RandomImages;

[TestFixture]
public class TenorServiceTest
{
    private IHttpService _httpService;
    private IConfiguration _configuration;
    private IRandomService _randomService;
    private TenorService _tenorService;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _randomService = Substitute.For<IRandomService>();

        _configuration.TenorApiKey.Returns("test-key");
        _randomService.NextInt(Arg.Any<int>()).Returns(0);

        _tenorService = new TenorService(_httpService, _configuration, _randomService);
    }

    private static IHttpResponse<TenorResponseDto> MakeResponse(params (string url, int w, int h)[] items)
    {
        var results = items.Select(item => new TenorResultDto
        {
            MediaFormats = new Dictionary<string, TenorMediaDto>
            {
                ["gif"] = new TenorMediaDto { Url = item.url, Dims = [item.w, item.h] }
            }
        }).ToList();
        return new HttpResponse<TenorResponseDto> { Data = new TenorResponseDto { Results = results } };
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnNull_WhenApiKeyIsEmpty()
    {
        _configuration.TenorApiKey.Returns(string.Empty);

        var result = await _tenorService.GetRandomMediaAsync("cats", "gif");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnNull_WhenHttpThrows()
    {
        _httpService.GetAsync<TenorResponseDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Throws(new Exception("network error"));

        var result = await _tenorService.GetRandomMediaAsync("cats", "gif");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnNull_WhenResultsAreEmpty()
    {
        _httpService.GetAsync<TenorResponseDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<TenorResponseDto> { Data = new TenorResponseDto { Results = [] } });

        var result = await _tenorService.GetRandomMediaAsync("cats", "gif");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnNull_WhenFormatNotPresent()
    {
        _httpService.GetAsync<TenorResponseDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(MakeResponse(("https://media.tenor.com/a.gif", 200, 100)));

        var result = await _tenorService.GetRandomMediaAsync("cats", "mp4");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnMediaInfo_WhenResultsAreValid()
    {
        _httpService.GetAsync<TenorResponseDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(MakeResponse(("https://media.tenor.com/a.gif", 200, 100)));

        var result = await _tenorService.GetRandomMediaAsync("cats", "gif");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Url, Is.EqualTo("https://media.tenor.com/a.gif"));
        Assert.That(result.Width, Is.EqualTo(200));
        Assert.That(result.Height, Is.EqualTo(100));
    }

    [Test]
    public async Task Test_GetMultipleMediaAsync_ShouldReturnEmptyList_WhenApiKeyIsEmpty()
    {
        _configuration.TenorApiKey.Returns(string.Empty);

        var result = await _tenorService.GetMultipleMediaAsync("cats", "gif", 4);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Test_GetMultipleMediaAsync_ShouldReturnEmptyList_WhenHttpThrows()
    {
        _httpService.GetAsync<TenorResponseDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Throws(new Exception("network error"));

        var result = await _tenorService.GetMultipleMediaAsync("cats", "gif", 4);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Test_GetMultipleMediaAsync_ShouldReturnUpToCountResults()
    {
        _httpService.GetAsync<TenorResponseDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(MakeResponse(
                ("https://media.tenor.com/a.gif", 200, 100),
                ("https://media.tenor.com/b.gif", 300, 150),
                ("https://media.tenor.com/c.gif", 400, 200),
                ("https://media.tenor.com/d.gif", 500, 250),
                ("https://media.tenor.com/e.gif", 600, 300),
                ("https://media.tenor.com/f.gif", 700, 350),
                ("https://media.tenor.com/g.gif", 800, 400),
                ("https://media.tenor.com/h.gif", 900, 450),
                ("https://media.tenor.com/i.gif", 1000, 500)));

        var result = await _tenorService.GetMultipleMediaAsync("cats", "gif", 8);

        Assert.That(result, Has.Count.EqualTo(8));
    }

    [Test]
    public async Task Test_GetMultipleMediaAsync_ShouldReturnAllAvailable_WhenFewerThanCount()
    {
        _httpService.GetAsync<TenorResponseDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(MakeResponse(
                ("https://media.tenor.com/a.gif", 200, 100),
                ("https://media.tenor.com/b.gif", 300, 150)));

        var result = await _tenorService.GetMultipleMediaAsync("cats", "gif", 4);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Test_GetMultipleMediaAsync_ShouldSkipResults_WhenFormatNotPresent()
    {
        var results = new List<TenorResultDto>
        {
            new() { MediaFormats = new Dictionary<string, TenorMediaDto> { ["gif"] = new() { Url = "https://media.tenor.com/a.gif", Dims = [200, 100] } } },
            new() { MediaFormats = new Dictionary<string, TenorMediaDto> { ["mp4"] = new() { Url = "https://media.tenor.com/b.mp4", Dims = [300, 150] } } }
        };
        _httpService.GetAsync<TenorResponseDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<TenorResponseDto> { Data = new TenorResponseDto { Results = results } });

        var result = await _tenorService.GetMultipleMediaAsync("cats", "gif", 4);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Url, Is.EqualTo("https://media.tenor.com/a.gif"));
    }
}
