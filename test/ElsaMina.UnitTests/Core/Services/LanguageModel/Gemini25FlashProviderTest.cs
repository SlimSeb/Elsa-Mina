using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.LanguageModel;
using ElsaMina.Core.Services.LanguageModel.Google;
using Newtonsoft.Json;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.LanguageModel;

[TestFixture]
public class Gemini25FlashProviderTest
{
    private static GeminiRequestDto ReadRequestBody(HttpRequest request) =>
        JsonConvert.DeserializeObject<GeminiRequestDto>(
            request.Body.CreateContent().ReadAsStringAsync().GetAwaiter().GetResult());

    private IHttpService _httpService;
    private IConfiguration _configuration;
    private Gemini25FlashProvider _languageModelProvider;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _languageModelProvider = new Gemini25FlashProvider(_configuration, _httpService);
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldReturnNull_WhenApiKeyIsMissing()
    {
        // Arrange
        _configuration.GeminiApiKey.Returns(string.Empty);

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync("test prompt");

        // Assert
        Assert.That(result, Is.Null);
        await _httpService.DidNotReceiveWithAnyArgs()
            .SendAsync<GeminiResponseDto>(default, default);
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldCallHttpService_WithCorrectParameters_ForPrompt()
    {
        // Arrange
        const string apiKey = "test-gemini-key";
        const string prompt = "test prompt";
        const string expectedResponse = "gemini response";

        _configuration.GeminiApiKey.Returns(apiKey);

        var geminiResponse = new GeminiResponseDto
        {
            Candidates =
            [
                new Candidate
                {
                    Content = new CandidateContent
                    {
                        Parts = [new CandidatePart { Text = expectedResponse }]
                    }
                }
            ]
        };

        _httpService
            .SendAsync<GeminiResponseDto>(
                Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<GeminiResponseDto> { Data = geminiResponse });

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync(prompt);

        // Assert
        Assert.That(result, Is.EqualTo(expectedResponse));
        await _httpService.Received(1).SendAsync<GeminiResponseDto>(
            Arg.Is<HttpRequest>(request =>
                request.Uri == "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent" &&
                ReadRequestBody(request).Contents[0].Parts[0].Text == prompt &&
                request.Headers["x-goog-api-key"] == apiKey),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldCallHttpService_WithSystemPromptAndConversation()
    {
        // Arrange
        const string apiKey = "test-gemini-key";
        _configuration.GeminiApiKey.Returns(apiKey);

        var geminiResponse = new GeminiResponseDto
        {
            Candidates =
            [
                new Candidate
                {
                    Content = new CandidateContent
                    {
                        Parts = [new CandidatePart { Text = "decision text" }]
                    }
                }
            ]
        };

        _httpService
            .SendAsync<GeminiResponseDto>(
                Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<GeminiResponseDto> { Data = geminiResponse });

        var request = new LanguageModelRequest
        {
            SystemPrompt = "System instruction",
            InputConversation =
            [
                new LanguageModelMessage { Role = MessageRole.User, Content = "User input" },
                new LanguageModelMessage { Role = MessageRole.Agent, Content = "Agent reply" }
            ]
        };

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync(request);

        // Assert
        Assert.That(result, Is.EqualTo("decision text"));
        await _httpService.Received(1).SendAsync<GeminiResponseDto>(
            Arg.Is<HttpRequest>(req =>
                ReadRequestBody(req).SystemInstruction.Parts[0].Text == "System instruction" &&
                ReadRequestBody(req).Contents[0].Role == "user" &&
                ReadRequestBody(req).Contents[0].Parts[0].Text == "User input" &&
                ReadRequestBody(req).Contents[1].Role == "model" &&
                ReadRequestBody(req).Contents[1].Parts[0].Text == "Agent reply"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldReturnEmpty_WhenResponseHasNoCandidates()
    {
        // Arrange
        _configuration.GeminiApiKey.Returns("test-gemini-key");

        var geminiResponse = new GeminiResponseDto { Candidates = [] };

        _httpService
            .SendAsync<GeminiResponseDto>(
                Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<GeminiResponseDto> { Data = geminiResponse });

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync("test prompt");

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }
}
