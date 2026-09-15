using ElsaMina.Commands.Misc.Wiki;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Wiki;

[TestFixture]
public class WikiMediaSearchCommandLeadingBlocksTest
{
    private const string PAGE_LINK = """ <a href="https://test.wiki/Page">Page</a><br>""";

    [NamedCommand("test-wiki-blocks")]
    private class TestableWikiMediaSearchCommand : WikiMediaSearchCommand
    {
        protected override string ApiUrl => "https://test.wiki/api.php";
        protected override string GetPageUrl(string title) => $"https://test.wiki/{title}";

        public override Rank RequiredRank => Rank.Regular;

        public TestableWikiMediaSearchCommand(IHttpService httpService)
            : base(httpService) { }
    }

    private IHttpService _httpService;
    private IContext _context;
    private TestableWikiMediaSearchCommand _command;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _context = Substitute.For<IContext>();
        _context.Target.Returns("test");
        _command = new TestableWikiMediaSearchCommand(_httpService);

        _httpService
            .SendAsync<WikipediaApiSearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<WikipediaApiSearchResponse>
            {
                Data = new WikipediaApiSearchResponse
                {
                    Query = new QueryPages
                    {
                        Pages = new Dictionary<string, WikiPage>
                        {
                            ["1"] = new() { PageId = 1, Title = "Page", Index = 1 }
                        }
                    }
                }
            });
        _httpService
            .SendAsync<WikipediaExtractResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<WikipediaExtractResponse>
            {
                Data = new WikipediaExtractResponse
                    { Query = new QueryWithExtract { Pages = new Dictionary<string, WikiExtractPage>() } }
            });
    }

    private void SetupWikitext(string wikitext)
    {
        _httpService
            .SendAsync<PokepediaParseResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<PokepediaParseResponse>
            {
                Data = new PokepediaParseResponse
                {
                    Parse = new PokepediaParseResult { Wikitext = new PokepediaWikitext { Content = wikitext } }
                }
            });
    }

    [Test]
    [TestCase("{{a}}Xrest of text", "rest of text")]
    [TestCase("{{{a}}}\n\nContent", "Content")]
    [TestCase("{{a}}}}\nContent", "} Content")]
    [TestCase("  \n{{Outer|{{Inner}}}}\n\nContent", "Content")]
    [TestCase("{|\n| cell {{x}}\n|}\nContent", "Content")]
    [TestCase("{|\n{|\n|}\n|}\nContent", "Content")]
    [TestCase("{| {{ |}\nContent", "Content")]
    [TestCase("{{a}}\n{|\n|}\n|}\n{{b|{{c}}}}\nContent here", "Content here")]
    [TestCase("|}\n\n|}\nContent", "Content")]
    [TestCase("{{abc\n\nReal text", "Real text")]
    [TestCase("{|abc\n\nReal text", "Real text")]
    public async Task Test_RunAsync_ShouldReplyWithExactExtract_WhenWikitextStartsWithBlocks(string wikitext,
        string expectedExtract)
    {
        // Arrange
        SetupWikitext(wikitext);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(expectedExtract + PAGE_LINK, rankAware: true);
    }

    [Test]
    [TestCase("|}")]
    [TestCase("{{a}}\n")]
    [TestCase("{|\n|}\n")]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenOnlyBlocksRemain(string wikitext)
    {
        // Arrange
        SetupWikitext(wikitext);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("wiki_page_not_found");
    }

    [Test]
    [TestCase("{{a}}")]
    [TestCase("{|\n|}")]
    public async Task Test_RunAsync_ShouldReplyError_WhenBlockClosesAtEndOfWikitext(string wikitext)
    {
        // Arrange
        SetupWikitext(wikitext);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("wiki_error");
    }
}
