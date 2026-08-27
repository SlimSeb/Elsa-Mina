using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.LanguageModel;
using ElsaMina.Core.Services.LanguageModel.OpenAi;
using Newtonsoft.Json;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.LanguageModel;

[TestFixture]
public class Gpt4OMiniProviderTest
{
    private static GptRequestDto ReadRequestBody(HttpRequest request) =>
        JsonConvert.DeserializeObject<GptRequestDto>(
            request.Body.CreateContent().ReadAsStringAsync().GetAwaiter().GetResult());

    private IHttpService _httpService;
    private IConfiguration _configuration;
    private Gpt4OMiniProvider _languageModelProvider;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _languageModelProvider = new Gpt4OMiniProvider(_httpService, _configuration);
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldReturnNull_WhenApiKeyIsMissing()
    {
        // Arrange
        _configuration.ChatGptApiKey.Returns(string.Empty);

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync("test prompt");

        // Assert
        Assert.That(result, Is.Null);
        await _httpService.DidNotReceiveWithAnyArgs()
            .SendAsync<GptResponseDto>(default, default);
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldCallHttpService_WithCorrectParameters()
    {
        // Arrange
        const string apiKey = "test-api-key";
        const string prompt = "test prompt";
        const string expectedResponse = "response content";

        _configuration.ChatGptApiKey.Returns(apiKey);

        var gptResponse = new GptResponseDto
        {
            Items =
            [
                new GptConversationItemDto
                {
                    Role = "assistant",
                    Content = expectedResponse
                }
            ]
        };

        _httpService
            .SendAsync<GptResponseDto>(
                Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<GptResponseDto> { Data = gptResponse });

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync(prompt);

        // Assert
        Assert.That(result, Is.EqualTo(expectedResponse));
        await _httpService.Received(1).SendAsync<GptResponseDto>(
            Arg.Is<HttpRequest>(request =>
                request.Uri == "https://api.openai.com/v1/chat/completions" &&
                ReadRequestBody(request).Messages[0].Content == prompt &&
                ReadRequestBody(request).Model == "gpt-4o-mini" &&
                request.Headers["Authorization"] == $"Bearer {apiKey}"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldCallHttpService_WithSystemPromptAndConversation()
    {
        // Arrange
        const string apiKey = "test-api-key";
        const string expectedResponse = "gpt conversation response";

        _configuration.ChatGptApiKey.Returns(apiKey);

        var gptResponse = new GptResponseDto
        {
            Items =
            [
                new GptConversationItemDto
                {
                    Role = "assistant",
                    Content = expectedResponse
                }
            ]
        };

        _httpService
            .SendAsync<GptResponseDto>(
                Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<GptResponseDto> { Data = gptResponse });

        var request = new LanguageModelRequest
        {
            SystemPrompt = "System instruction",
            InputConversation =
            [
                new LanguageModelMessage { Role = MessageRole.User, Content = "User input" },
                new LanguageModelMessage { Role = MessageRole.Agent, Content = "Agent output" }
            ]
        };

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync(request);

        // Assert
        Assert.That(result, Is.EqualTo(expectedResponse));
        await _httpService.Received(1).SendAsync<GptResponseDto>(
            Arg.Is<HttpRequest>(req =>
                ReadRequestBody(req).Model == "gpt-4o-mini" &&
                ReadRequestBody(req).Messages[0].Role == "system" &&
                ReadRequestBody(req).Messages[0].Content == "System instruction" &&
                ReadRequestBody(req).Messages[1].Role == "user" &&
                ReadRequestBody(req).Messages[1].Content == "User input" &&
                ReadRequestBody(req).Messages[2].Role == "assistant" &&
                ReadRequestBody(req).Messages[2].Content == "Agent output"),
            Arg.Any<CancellationToken>());
    }
}
