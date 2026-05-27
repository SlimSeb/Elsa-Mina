using ElsaMina.Commands.Ai.TextToSpeech;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.FileSharing;
using Newtonsoft.Json;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Ai.TextToSpeech;

public class ElevenLabsAiTextToSpeechProviderTest
{
    private static ElevenLabsRequestDto ReadRequestBody(HttpRequest request) =>
        JsonConvert.DeserializeObject<ElevenLabsRequestDto>(
            request.Body.CreateContent().ReadAsStringAsync().GetAwaiter().GetResult());

    private IConfiguration _mockConfiguration;
    private IHttpService _mockHttpService;
    private IFileSharingService _mockFileSharingService;
    private IClockService _mockClockService;
    private ElevenLabsAiTextToSpeechProvider _provider;

    [SetUp]
    public void SetUp()
    {
        _mockConfiguration = Substitute.For<IConfiguration>();
        _mockHttpService = Substitute.For<IHttpService>();
        _mockFileSharingService = Substitute.For<IFileSharingService>();
        _mockClockService = Substitute.For<IClockService>();
        _provider = new ElevenLabsAiTextToSpeechProvider(_mockConfiguration, _mockHttpService, _mockFileSharingService,
            _mockClockService);
    }

    [Test]
    public async Task Test_GetTextToSpeechAudioUrlAsync_ReturnsNull_WhenApiKeyIsMissing()
    {
        // Arrange
        _mockConfiguration.ElevenLabsApiKey.Returns(string.Empty);

        // Act
        var result = await _provider.GetTextToSpeechAudioUrlAsync("Test text", VoiceType.Female);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Test_GetTextToSpeechAudioUrlAsync_ReturnsUrl_WhenApiKeyIsValid()
    {
        // Arrange
        var mockStream = new MemoryStream();
        _mockConfiguration.ElevenLabsApiKey.Returns("valid-api-key");
        _mockHttpService.SendForStreamAsync(
            Arg.Any<HttpRequest>(),
            Arg.Any<CancellationToken>()).Returns(mockStream);
        _mockFileSharingService.CreateFileAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns("https://example.com/audio.mp3");

        // Act
        var result = await _provider.GetTextToSpeechAudioUrlAsync("Test text", VoiceType.Female);

        // Assert
        Assert.That(result, Is.EqualTo("https://example.com/audio.mp3"));
    }

    [Test]
    public async Task Test_GetTextToSpeechAudioUrlAsync_CallsHttpServiceWithCorrectParameters()
    {
        // Arrange
        var mockStream = new MemoryStream();
        _mockConfiguration.ElevenLabsApiKey.Returns("valid-api-key");
        _mockHttpService.SendForStreamAsync(
            Arg.Any<HttpRequest>(),
            Arg.Any<CancellationToken>()).Returns(mockStream);
        _mockFileSharingService.CreateFileAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns("https://example.com/audio.mp3");
        var cancellationToken = new CancellationToken();
        const string text = "Test text";

        // Act
        await _provider.GetTextToSpeechAudioUrlAsync(text, VoiceType.Male, cancellationToken);

        // Assert
        await _mockHttpService.Received(1).SendForStreamAsync(
            Arg.Is<HttpRequest>(request =>
                request.Uri == "https://api.elevenlabs.io/v1/text-to-speech/Qrl71rx6Yg8RvyPYRGCQ" &&
                ReadRequestBody(request).Text == text &&
                ReadRequestBody(request).ModelId == "eleven_multilingual_v2" &&
                request.Headers["xi-api-key"] == "valid-api-key"),
            cancellationToken);
    }
}