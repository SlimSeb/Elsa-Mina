using ElsaMina.Commands.Ai.TextToSpeech;
using ElsaMina.Core.Contexts;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Ai.TextToSpeech;

public class SpeakCommandTest
{
    private IAiTextToSpeechProvider _textToSpeechProvider;
    private IContext _context;

    private SpeakCommand _command;

    [SetUp]
    public void SetUp()
    {
        _textToSpeechProvider = Substitute.For<IAiTextToSpeechProvider>();
        _context = Substitute.For<IContext>();

        _command = new SpeakCommand(_textToSpeechProvider);
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenTargetIsNullOrEmpty(string target)
    {
        // Given
        _context.Target.Returns(target);

        // When
        await _command.RunAsync(_context);

        // Then
        _context.Received(1).Reply(_context.GetString("speak_help_message"));
        await _textToSpeechProvider
            .DidNotReceive()
            .GetTextToSpeechAudioUrlAsync(Arg.Any<string>(), Arg.Any<VoiceType>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReturnError_WhenTextToSpeechFailed()
    {
        // Given
        _context.Target.Returns("some text");
        _textToSpeechProvider
            .GetTextToSpeechAudioUrlAsync(Arg.Any<string>(), Arg.Any<VoiceType>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        // When
        await _command.RunAsync(_context);

        // Then
        _context.Received(1).ReplyLocalizedMessage("speak_error");
    }

    [Test]
    public async Task Test_RunAsync_ShouldFetchStreamAndUploadIt_WhenTtsAndFileUploadSucceeds()
    {
        // Given
        _context.Target.Returns("hello");
        _textToSpeechProvider
            .GetTextToSpeechAudioUrlAsync(Arg.Any<string>(), Arg.Any<VoiceType>(), Arg.Any<CancellationToken>())
            .Returns("https://ovh.net/s3/myaudio.mp3");

        // When
        await _command.RunAsync(_context);

        // Then
        _context.Received(1).ReplyHtml("""<audio src="https://ovh.net/s3/myaudio.mp3" controls aria-label="hello"></audio>""");
    }

    [Test]
    public async Task Test_RunAsync_ShouldFetchStreamAndUploadIt_WhenVoiceTypeIsDefined([Values] VoiceType voiceType)
    {
        // Given
        _context.Target.Returns($"text;; {voiceType}");
        _textToSpeechProvider
            .GetTextToSpeechAudioUrlAsync("text", voiceType, Arg.Any<CancellationToken>())
            .Returns("https://ovh.net/s3/myaudio.mp3");

        // When
        await _command.RunAsync(_context);

        // Then
        _context.Received(1).ReplyHtml("""<audio src="https://ovh.net/s3/myaudio.mp3" controls aria-label="text"></audio>""");
    }
    
    [Test]
    public async Task Test_RunAsync_ShouldFetchStreamAndUploadIt()
    {
        // Given
        _context.Target.Returns("text value, stuff, doing stuff");
        _textToSpeechProvider
            .GetTextToSpeechAudioUrlAsync("text value, stuff, doing stuff", VoiceType.Female, Arg.Any<CancellationToken>())
            .Returns("https://ovh.net/s3/myaudio.mp3");

        // When
        await _command.RunAsync(_context);

        // Then
        _context.Received(1).ReplyHtml("""<audio src="https://ovh.net/s3/myaudio.mp3" controls aria-label="text value, stuff, doing stuff"></audio>""");
    }
}