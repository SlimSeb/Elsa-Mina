using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Probabilities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.RandomImages;

[TestFixture]
public class KlipyServiceTest
{
    private IHttpService _httpService;
    private IConfiguration _configuration;
    private IRandomService _randomService;
    private KlipyService _klipyService;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _randomService = Substitute.For<IRandomService>();

        _configuration.KlipyApiKey.Returns("test-key");
        _randomService.NextInt(Arg.Any<int>()).Returns(0);

        _klipyService = new KlipyService(_httpService, _configuration, _randomService);
    }

    private static KlipyItem MakeItem(params (string size, string format, string url, int width, int height)[] files)
    {
        var file = new Dictionary<string, Dictionary<string, KlipyFile>>();
        foreach (var (size, format, url, width, height) in files)
        {
            if (!file.TryGetValue(size, out var formats))
            {
                formats = new Dictionary<string, KlipyFile>();
                file[size] = formats;
            }

            formats[format] = new KlipyFile { Url = url, Width = width, Height = height };
        }

        return new KlipyItem { File = file };
    }

    /// <summary>
    /// Builds an item carrying both the xs and sm gif variants, which is what a search hit needs.
    /// </summary>
    private static KlipyItem MakeSearchItem(string slug) => MakeItem(
        ("xs", "gif", $"https://static.klipy.com/{slug}-xs.gif", 90, 45),
        ("sm", "gif", $"https://static.klipy.com/{slug}-sm.gif", 220, 110));

    private static void SetUpResponse(IHttpService httpService, params KlipyItem[] items)
    {
        httpService.SendAsync<KlipySearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<KlipySearchResponse>
            {
                Data = new KlipySearchResponse
                {
                    Result = true,
                    Data = new KlipySearchData { Items = items.ToList() }
                }
            });
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnNull_WhenApiKeyIsEmpty()
    {
        _configuration.KlipyApiKey.Returns(string.Empty);

        var result = await _klipyService.GetRandomMediaAsync("cats", KlipyMediaSize.Md, KlipyMediaFormat.Gif);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnNull_WhenHttpThrows()
    {
        _httpService.SendAsync<KlipySearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("network error"));

        var result = await _klipyService.GetRandomMediaAsync("cats", KlipyMediaSize.Md, KlipyMediaFormat.Gif);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnNull_WhenResultsAreEmpty()
    {
        SetUpResponse(_httpService);

        var result = await _klipyService.GetRandomMediaAsync("cats", KlipyMediaSize.Md, KlipyMediaFormat.Gif);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnNull_WhenFormatNotPresent()
    {
        SetUpResponse(_httpService, MakeItem(("md", "gif", "https://static.klipy.com/a.gif", 200, 100)));

        var result = await _klipyService.GetRandomMediaAsync("cats", KlipyMediaSize.Md, KlipyMediaFormat.Mp4);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnNull_WhenSizeNotPresent()
    {
        SetUpResponse(_httpService, MakeItem(("md", "gif", "https://static.klipy.com/a.gif", 200, 100)));

        var result = await _klipyService.GetRandomMediaAsync("cats", KlipyMediaSize.Hd, KlipyMediaFormat.Gif);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldReturnMediaInfo_WhenResultsAreValid()
    {
        SetUpResponse(_httpService, MakeItem(("md", "gif", "https://static.klipy.com/a.gif", 200, 100)));

        var result = await _klipyService.GetRandomMediaAsync("cats", KlipyMediaSize.Md, KlipyMediaFormat.Gif);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("https://static.klipy.com/a.gif"));
            Assert.That(result.Width, Is.EqualTo(200));
            Assert.That(result.Height, Is.EqualTo(100));
        }
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldOnlyPickAmongMatchingItems_WhenSomeItemsLackTheFormat()
    {
        // The first item has no mp4, so a random index of 0 must still land on the second item.
        SetUpResponse(_httpService,
            MakeItem(("md", "gif", "https://static.klipy.com/a.gif", 200, 100)),
            MakeItem(("md", "mp4", "https://static.klipy.com/b.mp4", 300, 150)));

        var result = await _klipyService.GetRandomMediaAsync("cats", KlipyMediaSize.Md, KlipyMediaFormat.Mp4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Url, Is.EqualTo("https://static.klipy.com/b.mp4"));
        }
    }

    [Test]
    public async Task Test_GetRandomMediaAsync_ShouldRequestSearchEndpointWithApiKeyInPath()
    {
        SetUpResponse(_httpService, MakeItem(("md", "gif", "https://static.klipy.com/a.gif", 200, 100)));

        await _klipyService.GetRandomMediaAsync("cats", KlipyMediaSize.Md, KlipyMediaFormat.Gif);

        await _httpService.Received(1).SendAsync<KlipySearchResponse>(
            Arg.Is<HttpRequest>(request =>
                request.Uri == "https://api.klipy.com/api/v1/test-key/gifs/search"
                && request.QueryParameters["q"] == "cats"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_SearchAsync_ShouldReturnEmptyList_WhenApiKeyIsEmpty()
    {
        _configuration.KlipyApiKey.Returns(string.Empty);

        var result = await _klipyService.SearchAsync("cats", 4);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Test_SearchAsync_ShouldReturnEmptyList_WhenHttpThrows()
    {
        _httpService.SendAsync<KlipySearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("network error"));

        var result = await _klipyService.SearchAsync("cats", 4);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Test_SearchAsync_ShouldReturnUpToCountResults()
    {
        SetUpResponse(_httpService, Enumerable.Range(0, 9).Select(index => MakeSearchItem($"gif{index}")).ToArray());

        var result = await _klipyService.SearchAsync("cats", 8);

        Assert.That(result, Has.Count.EqualTo(8));
    }

    [Test]
    public async Task Test_SearchAsync_ShouldReturnAllAvailable_WhenFewerThanCount()
    {
        SetUpResponse(_httpService, MakeSearchItem("a"), MakeSearchItem("b"));

        var result = await _klipyService.SearchAsync("cats", 4);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Test_SearchAsync_ShouldPreserveApiOrdering()
    {
        SetUpResponse(_httpService, MakeSearchItem("a"), MakeSearchItem("b"), MakeSearchItem("c"));

        var result = await _klipyService.SearchAsync("cats", 8);

        Assert.That(result.Select(hit => hit.Full.Url), Is.EqualTo(new[]
        {
            "https://static.klipy.com/a-sm.gif",
            "https://static.klipy.com/b-sm.gif",
            "https://static.klipy.com/c-sm.gif"
        }));
    }

    [Test]
    public async Task Test_SearchAsync_ShouldReturnBothPreviewAndFullVariants()
    {
        SetUpResponse(_httpService, MakeSearchItem("a"));

        var result = await _klipyService.SearchAsync("cats", 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Preview.Url, Is.EqualTo("https://static.klipy.com/a-xs.gif"));
            Assert.That(result[0].Preview.Width, Is.EqualTo(90));
            Assert.That(result[0].Full.Url, Is.EqualTo("https://static.klipy.com/a-sm.gif"));
            Assert.That(result[0].Full.Width, Is.EqualTo(220));
        }
    }

    [Test]
    public async Task Test_SearchAsync_ShouldSkipItems_WhenAVariantIsMissing()
    {
        SetUpResponse(_httpService,
            MakeSearchItem("a"),
            MakeItem(("xs", "gif", "https://static.klipy.com/b-xs.gif", 90, 45)));

        var result = await _klipyService.SearchAsync("cats", 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Full.Url, Is.EqualTo("https://static.klipy.com/a-sm.gif"));
        }
    }

    [Test]
    public async Task Test_SearchAsync_ShouldClampPerPageToApiMinimum_WhenCountIsTooSmall()
    {
        SetUpResponse(_httpService, MakeSearchItem("a"));

        await _klipyService.SearchAsync("cats", 2);

        await _httpService.Received(1).SendAsync<KlipySearchResponse>(
            Arg.Is<HttpRequest>(request => request.QueryParameters["per_page"] == "8"),
            Arg.Any<CancellationToken>());
    }
}
