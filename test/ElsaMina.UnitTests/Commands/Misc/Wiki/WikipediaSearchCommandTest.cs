using System.Globalization;
using ElsaMina.Commands.Misc.Wiki;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Wiki;

public class WikipediaSearchCommandTest
{
    private IHttpService _mockHttpService;
    private IImageService _imageService;
    private WikipediaSearchCommand _command;

    [SetUp]
    public void SetUp()
    {
        _mockHttpService = Substitute.For<IHttpService>();
        _imageService = Substitute.For<IImageService>();
        _command = new WikipediaSearchCommand(_mockHttpService);
    }

    [Test]
    public void Test_Constructor_ShouldSetProperties()
    {
        // Act & Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
            Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
        }
    }

    [Test]
    public async Task RunAsync_ShouldReplyWithResults_WhenSearchReturnsResults()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Culture.Returns(new CultureInfo("fr-FR"));
        context.Target.Returns("Test");
        var searchResponse = new WikipediaApiSearchResponse
        {
            Query = new QueryPages
            {
                Pages = new Dictionary<string, WikiPage>
                {
                    { "1", new WikiPage { PageId = 1, Title = "Test Page" } }
                }
            }
        };
        var extractResponse = new WikipediaExtractResponse
        {
            Query = new QueryWithExtract
            {
                Pages = new Dictionary<string, WikiExtractPage>
                {
                    { "1", new WikiExtractPage { PageId = 1, Title = "Test Page", Extract = "Summary line." } }
                }
            }
        };
        _mockHttpService.SendAsync<WikipediaApiSearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<WikipediaApiSearchResponse> { Data = searchResponse });
        _mockHttpService.SendAsync<WikipediaExtractResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<WikipediaExtractResponse> { Data = extractResponse });

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyHtml(Arg.Is<string>(s => s.Contains("Test Page") && s.Contains("Summary line.")), rankAware: true);
    }

    [Test]
    public async Task RunAsync_ShouldReplyWithNoResultsMessage_WhenSearchReturnsEmptyResults()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Culture.Returns(new CultureInfo("fr-FR"));
        context.Target.Returns("Test");
        var searchResponse = new WikipediaApiSearchResponse
        {
            Query = new QueryPages
            {
                Pages = new Dictionary<string, WikiPage>()
            }
        };
        _mockHttpService.SendAsync<WikipediaApiSearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<WikipediaApiSearchResponse> { Data = searchResponse });

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyRankAwareLocalizedMessage("wiki_page_not_found");
    }

    [Test]
    public async Task RunAsync_ShouldReplyWithErrorMessage_WhenHttpRequestFails()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Culture.Returns(new CultureInfo("fr-FR"));
        context.Target.Returns("Test");
        _mockHttpService.SendAsync<WikipediaApiSearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Error"));

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).ReplyRankAwareLocalizedMessage("wiki_error", "Error");
    }
}