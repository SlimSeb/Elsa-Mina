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
    private Gpt4OMiniProvider _gptProvider;
    private Gemini25FlashProvider _geminiProvider;
    private MistralSmallProvider _mistralProvider;
    private LanguageModelResolver _resolver;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _dependencyContainer = Substitute.For<IDependencyContainerService>();

        _gptProvider = Substitute.ForPartsOf<Gpt4OMiniProvider>(null, _configuration);
        _geminiProvider = Substitute.ForPartsOf<Gemini25FlashProvider>(_configuration, null);
        _mistralProvider = Substitute.ForPartsOf<MistralSmallProvider>(null, _configuration);

        _dependencyContainer.Resolve<Gpt4OMiniProvider>().Returns(_gptProvider);
        _dependencyContainer.Resolve<Gemini25FlashProvider>().Returns(_geminiProvider);
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
    public async Task Test_AskLanguageModelAsync_ShouldCallGptFirst_WhenGptKeyIsConfigured()
    {
        // Arrange
        _configuration.ChatGptApiKey.Returns("gpt-key");
        _configuration.GeminiApiKey.Returns("gemini-key");

        _gptProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .Returns("gpt response");

        // Act
        var result = await _resolver.AskLanguageModelAsync("hello");

        // Assert
        Assert.That(result, Is.EqualTo("gpt response"));
        await _gptProvider.Received(1).AskLanguageModelAsync("hello", Arg.Any<CancellationToken>());
        await _geminiProvider.DidNotReceiveWithAnyArgs().AskLanguageModelAsync(Arg.Any<string>());
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldFallbackToGemini_WhenGptFails()
    {
        // Arrange
        _configuration.ChatGptApiKey.Returns("gpt-key");
        _configuration.GeminiApiKey.Returns("gemini-key");

        _gptProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("GPT failed"));
        _geminiProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .Returns("gemini response");

        // Act
        var result = await _resolver.AskLanguageModelAsync("hello");

        // Assert
        Assert.That(result, Is.EqualTo("gemini response"));
        await _gptProvider.Received(1).AskLanguageModelAsync("hello", Arg.Any<CancellationToken>());
        await _geminiProvider.Received(1).AskLanguageModelAsync("hello", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_AskLanguageModelAsync_ShouldFallbackToMistral_WhenGptAndGeminiFail()
    {
        // Arrange
        _configuration.ChatGptApiKey.Returns("gpt-key");
        _configuration.GeminiApiKey.Returns("gemini-key");
        _configuration.MistralApiKey.Returns("mistral-key");

        _gptProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .Returns(string.Empty);
        _geminiProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Gemini error"));
        _mistralProvider.AskLanguageModelAsync("hello", Arg.Any<CancellationToken>())
            .Returns("mistral response");

        // Act
        var result = await _resolver.AskLanguageModelAsync("hello");

        // Assert
        Assert.That(result, Is.EqualTo("mistral response"));
        await _mistralProvider.Received(1).AskLanguageModelAsync("hello", Arg.Any<CancellationToken>());
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
