using ElsaMina.Commands.Misc.Dictionary;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Dictionary;

public class DictionaryCommandTest
{
    private IHttpService _httpService;
    private IConfiguration _configuration;
    private IContext _context;
    private DictionaryCommand _command;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();
        _command = new DictionaryCommand(_httpService, _configuration);
    }

    private static IHttpResponse<DictionaryApiResponse> MakeEntryResponse(string word, params string[] shortDefs) =>
        new HttpResponse<DictionaryApiResponse>
        {
            Data = new DictionaryApiResponse
            {
                Entries =
                [
                    new DictionaryApiEntry
                    {
                        ShortDefinitions = shortDefs.ToList(),
                        HeadwordInfo = new DictionaryApiHeadwordInfo { Headword = word }
                    }
                ]
            }
        };

    private static IHttpResponse<DictionaryApiResponse> MakeSuggestionsResponse(params string[] suggestions) =>
        new HttpResponse<DictionaryApiResponse>
        {
            Data = new DictionaryApiResponse { Suggestions = suggestions.ToList() }
        };

    private static IHttpResponse<DictionaryApiResponse> MakeEmptyResponse() =>
        new HttpResponse<DictionaryApiResponse> { Data = new DictionaryApiResponse() };

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).GetString(_command.HelpMessageKey);
        await _httpService.DidNotReceive()
            .SendAsync<DictionaryApiResponse>(Arg.Any<HttpRequest>());
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task Test_RunAsync_ShouldNotCallApi_WhenApiKeyIsNotConfigured(string emptyApiKey)
    {
        // Arrange
        _context.Target.Returns("president");
        _configuration.DictionaryApiKey.Returns(emptyApiKey);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _httpService.DidNotReceive()
            .SendAsync<DictionaryApiResponse>(Arg.Any<HttpRequest>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyDefinition_WhenWordIsFound()
    {
        // Arrange
        _context.Target.Returns("president");
        _configuration.DictionaryApiKey.Returns("api-key");
        _httpService
            .SendAsync<DictionaryApiResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeEntryResponse("president", "one who presides over a meeting or assembly"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("dicoenglish_definition", "president",
            "one who presides over a meeting or assembly");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySuggestions_WhenWordIsNotFound()
    {
        // Arrange
        _context.Target.Returns("presidnet");
        _configuration.DictionaryApiKey.Returns("api-key");
        _httpService
            .SendAsync<DictionaryApiResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeSuggestionsResponse("president", "precedent", "resident"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("dicoenglish_suggestions", "presidnet",
            "president, precedent, resident");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenResponseIsEmpty()
    {
        // Arrange
        _context.Target.Returns("xyzzy");
        _configuration.DictionaryApiKey.Returns("api-key");
        _httpService
            .SendAsync<DictionaryApiResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeEmptyResponse());

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("dicoenglish_not_found", "xyzzy");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenEntryHasNoShortDefinitions()
    {
        // Arrange
        _context.Target.Returns("president");
        _configuration.DictionaryApiKey.Returns("api-key");
        _httpService
            .SendAsync<DictionaryApiResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeEntryResponse("president"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("dicoenglish_not_found", "president");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenExceptionOccurs()
    {
        // Arrange
        _context.Target.Returns("president");
        _configuration.DictionaryApiKey.Returns("api-key");
        _httpService
            .SendAsync<DictionaryApiResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Network error"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("dicoenglish_error");
    }
}
