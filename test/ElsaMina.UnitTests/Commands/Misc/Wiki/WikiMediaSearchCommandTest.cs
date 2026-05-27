using ElsaMina.Commands.Misc.Wiki;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Wiki;

[TestFixture]
public class WikiMediaSearchCommandTest
{
    // Minimal concrete subclass used to exercise the abstract base class
    [NamedCommand("test-wiki")]
    private class TestableWikiMediaSearchCommand : WikiMediaSearchCommand
    {
        protected override string ApiUrl => "https://test.wiki/api.php";
        protected override string GetPageUrl(string title) => $"https://test.wiki/{title}";

        public override Rank RequiredRank => Rank.Regular;
        public override bool IsAllowedInPrivateMessage => true;

        public TestableWikiMediaSearchCommand(IHttpService httpService)
            : base(httpService) { }
    }

    private IHttpService _mockHttpService;
    private TestableWikiMediaSearchCommand _command;

    [SetUp]
    public void SetUp()
    {
        _mockHttpService = Substitute.For<IHttpService>();
        _command = new TestableWikiMediaSearchCommand(_mockHttpService);
    }

    private void SetupSearchResponse(int pageId = 1, string title = "Test Page",
        IDictionary<string, string> pageProps = null)
    {
        var response = new WikipediaApiSearchResponse
        {
            Query = new QueryPages
            {
                Pages = new Dictionary<string, WikiPage>
                {
                    { pageId.ToString(), new WikiPage { PageId = pageId, Title = title, Index = 1, PageProps = pageProps } }
                }
            }
        };
        _mockHttpService
            .SendAsync<WikipediaApiSearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<WikipediaApiSearchResponse> { Data = response });
    }

    private void SetupParseResponse(string wikitext)
    {
        var response = new PokepediaParseResponse
        {
            Parse = new PokepediaParseResult
            {
                Wikitext = new PokepediaWikitext { Content = wikitext }
            }
        };
        _mockHttpService
            .SendAsync<PokepediaParseResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<PokepediaParseResponse> { Data = response });
    }

    private void SetupThumbnailResponse(Thumbnail thumbnail = null)
    {
        var pages = thumbnail == null
            ? new Dictionary<string, WikiExtractPage>()
            : new Dictionary<string, WikiExtractPage> { { "1", new WikiExtractPage { Thumbnail = thumbnail } } };
        var response = new WikipediaExtractResponse { Query = new QueryWithExtract { Pages = pages } };
        _mockHttpService
            .SendAsync<WikipediaExtractResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<WikipediaExtractResponse> { Data = response });
    }

    private IContext MakeContext(string target = "test")
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns(target);
        return context;
    }

    // --- RunAsync: happy path ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHtml_WhenPageFoundWithExtract()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse(title: "Pikachu");
        SetupParseResponse("'''Pikachu''' is an Electric-type Pokémon.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("Pikachu") && s.Contains("Electric-type Pokémon")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeImageTag_WhenThumbnailIsPresent()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse();
        SetupParseResponse("Some description.");
        SetupThumbnailResponse(new Thumbnail { Source = "https://img.example.com/pic.png", Width = 300, Height = 200 });

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("<img") && s.Contains("pic.png")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotIncludeImageTag_WhenThumbnailIsAbsent()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse();
        SetupParseResponse("Some description.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => !s.Contains("<img")),
            rankAware: true);
    }

    // --- RunAsync: not found ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNotFound_WhenSearchReturnsNoPages()
    {
        // Arrange
        var context = MakeContext();
        _mockHttpService
            .SendAsync<WikipediaApiSearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<WikipediaApiSearchResponse>
            {
                Data = new WikipediaApiSearchResponse { Query = new QueryPages { Pages = new Dictionary<string, WikiPage>() } }
            });

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyRankAwareLocalizedMessage("wiki_page_not_found");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNotFound_WhenAllPagesAreDisambiguation()
    {
        // Arrange
        var context = MakeContext();
        var response = new WikipediaApiSearchResponse
        {
            Query = new QueryPages
            {
                Pages = new Dictionary<string, WikiPage>
                {
                    { "1", new WikiPage { PageId = 1, Title = "Test", Index = 1, PageProps = new Dictionary<string, string> { ["disambiguation"] = "" } } }
                }
            }
        };
        _mockHttpService
            .SendAsync<WikipediaApiSearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<WikipediaApiSearchResponse> { Data = response });

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyRankAwareLocalizedMessage("wiki_page_not_found");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNotFound_WhenWikitextIsEmpty()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse();
        SetupParseResponse(string.Empty);
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyRankAwareLocalizedMessage("wiki_page_not_found");
    }

    // --- RunAsync: error ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenHttpThrows()
    {
        // Arrange
        var context = MakeContext();
        _mockHttpService
            .SendAsync<WikipediaApiSearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("network failure"));

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyRankAwareLocalizedMessage("wiki_error");
    }

    // --- Wikitext parsing (tested through RunAsync) ---

    [Test]
    public async Task Test_RunAsync_ShouldStripSimpleInfobox_WhenWikitextStartsWithTemplate()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse(title: "Sinnoh");
        SetupParseResponse("{{Infobox Région\n| nom=Sinnoh\n}}\n\n'''Sinnoh''' est une région du monde Pokémon.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("Sinnoh est une région") && !s.Contains("{{") && !s.Contains("}}")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldStripNestedInfoboxes_WhenWikitextHasNestedTemplates()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse(title: "Test");
        SetupParseResponse("{{Outer\n|field={{Inner|value}}\n}}\n\n'''Result''' is here.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("Result is here") && !s.Contains("{{")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldStripStrayTableClose_WhenWikitextHasBulbapediaStyle()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse(title: "Cynthia");
        SetupParseResponse("{{Character Infobox\n|name=Cynthia\n}}\n|}  \n\n'''Cynthia''' is the Champion of Sinnoh.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("Cynthia is the Champion") && !s.Contains("|}")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCleanWikiLinks_WhenExtractContainsPipedLinks()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse();
        SetupParseResponse("[[Pokémon|monster]] are found everywhere.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("monster are found") && !s.Contains("[[")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCleanWikiLinks_WhenExtractContainsSimpleLinks()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse();
        SetupParseResponse("[[Pikachu]] is an Electric-type.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("Pikachu is an Electric-type") && !s.Contains("[[")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCleanTemplates_WhenExtractContainsSimpleTemplates()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse();
        SetupParseResponse("Appears in {{Jeu|Diamond and Pearl}}.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("Diamond and Pearl") && !s.Contains("{{")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCleanBoldMarkup_WhenExtractContainsTripleApostrophes()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse();
        SetupParseResponse("'''Pikachu''' is a Pokémon.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("Pikachu is a Pokémon") && !s.Contains("'''")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSkipHeadings_WhenFirstContentIsASectionHeader()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse();
        SetupParseResponse("== Overview ==\n\nActual content here.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("Actual content here") && !s.Contains("==")),
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldRemoveFileLinks_WhenExtractContainsFileEmbeds()
    {
        // Arrange
        var context = MakeContext();
        SetupSearchResponse();
        SetupParseResponse("[[File:Pikachu.png|thumb]] Some description.");
        SetupThumbnailResponse();

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(
            Arg.Is<string>(s => s.Contains("Some description") && !s.Contains("File:")),
            rankAware: true);
    }
}
