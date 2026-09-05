using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.LanguageModel;
using ElsaMina.Core.Services.LanguageModel.Google;
using ElsaMina.Core.Services.LanguageModel.Mistral;
using ElsaMina.Core.Services.LanguageModel.OpenAi;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Core.Services.LanguageModel;

[TestFixture]
public class LanguageModelResolverTest
{
    private IConfiguration _configuration;
    private IDependencyContainerService _dependencyContainer;
    private GptMiniProvider _gptProvider;
    private GeminiFlashProvider _geminiProvider;
    private MistralSmallProvider _mistralProvider;
    private LanguageModelResolver _resolver;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _dependencyContainer = Substitute.For<IDependencyContainerService>();

        _gptProvider = Substitute.ForPartsOf<GptMiniProvider>(null, _configuration);
        _geminiProvider = Substitute.ForPartsOf<GeminiFlashProvider>(_configuration, null);
        _mistralProvider = Substitute.ForPartsOf<MistralSmallProvider>(null, _configuration);

        _dependencyContainer.Resolve<GptMiniProvider>().Returns(_gptProvider);
        _dependencyContainer.Resolve<GeminiFlashProvider>().Returns(_geminiProvider);
        _dependencyContainer.Resolve<MistralSmallProvider>().Returns(_mistralProvider);

        _resolver = new LanguageModelResolver(_configuration, _dependencyContainer);
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldReturnNull_WhenNoApiKeysConfigured()
    {
        // Arrange
        _configuration.ChatGptApiKey.Returns(string.Empty);
        _configuration.GeminiApiKey.Returns(string.Empty);
        _configuration.MistralApiKey.Returns(string.Empty);

        // Act
        var result = await _resolver.AskLanguageModelAsync("hello");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldCallGeminiFirst_WhenGeminiKeyIsConfigured()
    {
        // Arrange
        _configuration.ChatGptApiKey.Returns("gpt-key");
        _configuration.GeminiApiKey.Returns("gemini-key");

        _geminiProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .Returns("gemini response");

        // Act
        var result = await _resolver.AskLanguageModelAsync("hello");

        // Assert
        Assert.That(result, Is.EqualTo("gemini response"));
        await _geminiProvider.Received(1).AskLanguageModelAsync("hello", Arg.Any<CancellationToken>());
        await _gptProvider.DidNotReceive().AskLanguageModelAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldFallbackToMistral_WhenGeminiFails()
    {
        // Arrange
        _configuration.GeminiApiKey.Returns("gemini-key");
        _configuration.MistralApiKey.Returns("mistral-key");

        _geminiProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Gemini failed"));
        _mistralProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .Returns("mistral response");

        // Act
        var result = await _resolver.AskLanguageModelAsync("hello");

        // Assert
        Assert.That(result, Is.EqualTo("mistral response"));
        await _geminiProvider.Received(1).AskLanguageModelAsync("hello", Arg.Any<CancellationToken>());
        await _mistralProvider.Received(1).AskLanguageModelAsync("hello", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldFallbackToGpt_WhenGeminiAndMistralFail()
    {
        // Arrange
        _configuration.ChatGptApiKey.Returns("gpt-key");
        _configuration.GeminiApiKey.Returns("gemini-key");
        _configuration.MistralApiKey.Returns("mistral-key");

        _geminiProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .Returns(string.Empty);
        _mistralProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Mistral error"));
        _gptProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .Returns("gpt response");

        // Act
        var result = await _resolver.AskLanguageModelAsync("hello");

        // Assert
        Assert.That(result, Is.EqualTo("gpt response"));
        await _geminiProvider.Received(1).AskLanguageModelAsync("hello", Arg.Any<CancellationToken>());
        await _mistralProvider.Received(1).AskLanguageModelAsync("hello", Arg.Any<CancellationToken>());
        await _gptProvider.Received(1).AskLanguageModelAsync("hello", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_WithRequest_ShouldForwardToProvider()
    {
        // Arrange
        _configuration.ChatGptApiKey.Returns("gpt-key");
        var request = new LanguageModelRequest
        {
            SystemPrompt = "System prompt",
            InputConversation = [new LanguageModelMessage { Role = MessageRole.User, Content = "Hi" }]
        };

        _gptProvider.AskLanguageModelAsync(request, Arg.Any<CancellationToken>())
            .Returns("conversation response");

        // Act
        var result = await _resolver.AskLanguageModelAsync(request);

        // Assert
        Assert.That(result, Is.EqualTo("conversation response"));
        await _gptProvider.Received(1).AskLanguageModelAsync(request, Arg.Any<CancellationToken>());
    }
}
