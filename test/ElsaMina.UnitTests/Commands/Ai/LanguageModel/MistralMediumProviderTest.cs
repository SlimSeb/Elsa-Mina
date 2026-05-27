using ElsaMina.Commands.Ai.LanguageModel.Mistral;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using Newtonsoft.Json;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Ai.LanguageModel;

[TestFixture]
public class MistralMediumProviderTest
{
    private static MistralRequestDto ReadRequestBody(HttpRequest request) =>
        JsonConvert.DeserializeObject<MistralRequestDto>(
            request.Body.CreateContent().ReadAsStringAsync().GetAwaiter().GetResult());

    private IHttpService _httpService;
    private IConfiguration _configuration;
    private MistralMediumProvider _languageModelProvider;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _languageModelProvider = new MistralMediumProvider(_httpService, _configuration);
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldReturnNull_WhenApiKeyIsMissing()
    {
        // Arrange
        _configuration.MistralApiKey.Returns(string.Empty);

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync("test prompt");

        // Assert
        Assert.That(result, Is.Null);
        await _httpService.DidNotReceiveWithAnyArgs()
            .SendAsync<MistralResponseDto>(default, default);
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldCallHttpService_WithCorrectParameters()
    {
        // Arrange
        const string apiKey = "test-api-key";
        const string prompt = "test prompt";
        const string expectedResponse = "response content";

        _configuration.MistralApiKey.Returns(apiKey);

        var mistralResponse = new MistralResponseDto
        {
            Choices =
            [
                new MistralChoiceDto
                {
                    Message = new MistralResponseMessageDto
                    {
                        Content = expectedResponse
                    }
                }
            ]
        };

        _httpService
            .SendAsync<MistralResponseDto>(
                Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<MistralResponseDto> { Data = mistralResponse });

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync(prompt);

        // Assert
        Assert.That(result, Is.EqualTo(expectedResponse));
        await _httpService.Received(1).SendAsync<MistralResponseDto>(
            Arg.Is<HttpRequest>(request =>
                request.Uri == "https://api.mistral.ai/v1/chat/completions" &&
                ReadRequestBody(request).Messages[0].Content == prompt &&
                request.Headers["Authorization"] == $"Bearer {apiKey}"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldReturnNull_WhenHttpResponseIsNull()
    {
        // Arrange
        _configuration.MistralApiKey.Returns("test-api-key");

        _httpService
            .SendAsync<MistralResponseDto>(
                Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns((HttpResponse<MistralResponseDto>)null);

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync("test prompt");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldReturnNull_WhenNoChoicesArePresent()
    {
        // Arrange
        _configuration.MistralApiKey.Returns("test-api-key");

        var mistralResponse = new MistralResponseDto { Choices = null };

        _httpService
            .SendAsync<MistralResponseDto>(
                Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<MistralResponseDto> { Data = mistralResponse });

        // Act
        var result = await _languageModelProvider.AskLanguageModelAsync("test prompt");

        // Assert
        Assert.That(result, Is.Null);
    }
}