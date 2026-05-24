using ElsaMina.Core.Services.CustomColors;
using ElsaMina.Core.Services.Http;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Core.Services.CustomColors;

public class CustomColorsManagerTest
{
    private IHttpService _httpService;
    private CustomColorsManager _customColorsManager;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _customColorsManager = new CustomColorsManager(_httpService);
    }

    private const string VALID_JS = """
        Config.customcolors = {
          'jsuser1': 'jscolor1',
          'jsuser2': ''
        };
        """;

    private static IHttpResponse<Dictionary<string, string>> JsonResponse(Dictionary<string, string> data) =>
        new HttpResponse<Dictionary<string, string>> { Data = data };

    private static IHttpResponse<string> JsResponse(string js) =>
        new HttpResponse<string> { Data = js };

    [Test]
    public async Task Test_FetchCustomColorsAsync_ShouldMergeBothSources_WhenBothSucceed()
    {
        var jsonColors = new Dictionary<string, string> { { "jsonuser", "#FF5733" } };
        _httpService.GetAsync<Dictionary<string, string>>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JSON_URL))
            .Returns(JsonResponse(jsonColors));
        _httpService.GetAsync<string>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JS_URL), isRaw: true)
            .Returns(JsResponse(VALID_JS));

        await _customColorsManager.FetchCustomColorsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_customColorsManager.CustomColorsMapping, Has.Count.EqualTo(3));
            Assert.That(_customColorsManager.CustomColorsMapping["jsonuser"], Is.EqualTo("#FF5733"));
            Assert.That(_customColorsManager.CustomColorsMapping["jsuser1"], Is.EqualTo("jscolor1"));
            Assert.That(_customColorsManager.CustomColorsMapping["jsuser2"], Is.EqualTo(""));
        }
    }

    [Test]
    public async Task Test_FetchCustomColorsAsync_ShouldKeepJsonEntry_WhenKeyExistsInBothSources()
    {
        var jsonColors = new Dictionary<string, string> { { "shareduser", "json-value" } };
        var js = """
            Config.customcolors = {
              'shareduser': 'js-value'
            };
            """;
        _httpService.GetAsync<Dictionary<string, string>>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JSON_URL))
            .Returns(JsonResponse(jsonColors));
        _httpService.GetAsync<string>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JS_URL), isRaw: true)
            .Returns(JsResponse(js));

        await _customColorsManager.FetchCustomColorsAsync();

        Assert.That(_customColorsManager.CustomColorsMapping["shareduser"], Is.EqualTo("json-value"));
    }

    [Test]
    public async Task Test_FetchCustomColorsAsync_ShouldStillFetchJs_WhenJsonFails()
    {
        _httpService.GetAsync<Dictionary<string, string>>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JSON_URL))
            .Throws(new Exception("Network error"));
        _httpService.GetAsync<string>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JS_URL), isRaw: true)
            .Returns(JsResponse(VALID_JS));

        await _customColorsManager.FetchCustomColorsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_customColorsManager.CustomColorsMapping, Has.Count.EqualTo(2));
            Assert.That(_customColorsManager.CustomColorsMapping["jsuser1"], Is.EqualTo("jscolor1"));
        }
    }

    [Test]
    public async Task Test_FetchCustomColorsAsync_ShouldStillFetchJson_WhenJsFails()
    {
        var jsonColors = new Dictionary<string, string> { { "jsonuser", "#FF5733" } };
        _httpService.GetAsync<Dictionary<string, string>>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JSON_URL))
            .Returns(JsonResponse(jsonColors));
        _httpService.GetAsync<string>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JS_URL), isRaw: true)
            .Throws(new Exception("Network error"));

        await _customColorsManager.FetchCustomColorsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_customColorsManager.CustomColorsMapping, Has.Count.EqualTo(1));
            Assert.That(_customColorsManager.CustomColorsMapping["jsonuser"], Is.EqualTo("#FF5733"));
        }
    }

    [Test]
    public async Task Test_FetchCustomColorsAsync_ShouldLeaveEmptyMapping_WhenBothSourcesFail()
    {
        _httpService.GetAsync<Dictionary<string, string>>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JSON_URL))
            .Throws(new Exception("Network error"));
        _httpService.GetAsync<string>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JS_URL), isRaw: true)
            .Throws(new Exception("Network error"));

        await _customColorsManager.FetchCustomColorsAsync();

        Assert.That(_customColorsManager.CustomColorsMapping, Is.Empty);
    }

    [Test]
    public void Test_FetchCustomColorsAsync_ShouldNotThrow_WhenBothSourcesFail()
    {
        _httpService.GetAsync<Dictionary<string, string>>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JSON_URL))
            .Throws(new Exception("Network error"));
        _httpService.GetAsync<string>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JS_URL), isRaw: true)
            .Throws(new Exception("Network error"));

        Assert.DoesNotThrowAsync(async () => await _customColorsManager.FetchCustomColorsAsync());
    }

    [Test]
    public async Task Test_FetchCustomColorsAsync_ShouldLeaveEmptyJsEntries_WhenJsBlockIsAbsent()
    {
        _httpService.GetAsync<Dictionary<string, string>>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JSON_URL))
            .Throws(new Exception("Network error"));
        _httpService.GetAsync<string>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JS_URL), isRaw: true)
            .Returns(JsResponse("var x = 1;"));

        await _customColorsManager.FetchCustomColorsAsync();

        Assert.That(_customColorsManager.CustomColorsMapping, Is.Empty);
    }

    [Test]
    public async Task Test_FetchCustomColorsAsync_ShouldIncludeCommentedEntries_WhenJsHasInlineComments()
    {
        // The regex does not parse JS syntax, so entries inside // comments are still matched.
        var jsWithComments = """
            Config.customcolors = {
              'user1': 'color1', // some inline comment
              // 'commented': 'out',
              'user2': 'color2'
            };
            """;
        _httpService.GetAsync<Dictionary<string, string>>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JSON_URL))
            .Throws(new Exception("Network error"));
        _httpService.GetAsync<string>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JS_URL), isRaw: true)
            .Returns(JsResponse(jsWithComments));

        await _customColorsManager.FetchCustomColorsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_customColorsManager.CustomColorsMapping, Has.Count.EqualTo(3));
            Assert.That(_customColorsManager.CustomColorsMapping.ContainsKey("user1"), Is.True);
            Assert.That(_customColorsManager.CustomColorsMapping.ContainsKey("user2"), Is.True);
            Assert.That(_customColorsManager.CustomColorsMapping.ContainsKey("commented"), Is.True);
        }
    }

    [Test]
    public async Task Test_FetchCustomColorsAsync_ShouldKeepFirstEntry_WhenJsBlockHasDuplicateKeys()
    {
        var jsWithDuplicates = """
            Config.customcolors = {
              'user1': 'first',
              'user1': 'second'
            };
            """;
        _httpService.GetAsync<Dictionary<string, string>>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JSON_URL))
            .Throws(new Exception("Network error"));
        _httpService.GetAsync<string>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JS_URL), isRaw: true)
            .Returns(JsResponse(jsWithDuplicates));

        await _customColorsManager.FetchCustomColorsAsync();

        Assert.That(_customColorsManager.CustomColorsMapping["user1"], Is.EqualTo("first"));
    }

    [Test]
    public async Task Test_FetchCustomColorsAsync_ShouldHandleEmptyStringValues_WhenJsonHasEmptyValues()
    {
        var jsonColors = new Dictionary<string, string> { { "user1", "" }, { "user2", "color2" } };
        _httpService.GetAsync<Dictionary<string, string>>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JSON_URL))
            .Returns(JsonResponse(jsonColors));
        _httpService.GetAsync<string>(Arg.Is(CustomColorsManager.CUSTOM_COLORS_JS_URL), isRaw: true)
            .Throws(new Exception("Network error"));

        await _customColorsManager.FetchCustomColorsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_customColorsManager.CustomColorsMapping["user1"], Is.EqualTo(""));
            Assert.That(_customColorsManager.CustomColorsMapping["user2"], Is.EqualTo("color2"));
        }
    }
}
