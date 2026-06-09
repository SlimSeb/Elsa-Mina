using System.Globalization;
using ElsaMina.Commands.Ai.Chat;
using ElsaMina.Commands.Ai.LanguageModel;
using ElsaMina.Commands.Ai.TextToSpeech;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Ai.Chat;

public class AskElsaCommandTest
{
    private IConfiguration _mockConfiguration;
    private IResourcesService _mockResourcesService;
    private IPersonalityService _mockPersonalityService;
    private ILanguageModelProvider _mockLanguageModelProvider;
    private IAiTextToSpeechProvider _mockTextToSpeechProvider;
    private IConversationHistoryService _mockConversationHistory;
    private AskElsaCommand _command;

    [SetUp]
    public void SetUp()
    {
        _mockConfiguration = Substitute.For<IConfiguration>();
        _mockConfiguration.Name.Returns("Elsa");
        _mockResourcesService = Substitute.For<IResourcesService>();
        _mockPersonalityService = Substitute.For<IPersonalityService>();
        _mockPersonalityService.GetPersonality(Arg.Any<string>()).Returns(BotPersonality.Silly);
        _mockLanguageModelProvider = Substitute.For<ILanguageModelProvider>();
        _mockTextToSpeechProvider = Substitute.For<IAiTextToSpeechProvider>();
        _mockConversationHistory = Substitute.For<IConversationHistoryService>();
        _mockConversationHistory.BuildConversation(Arg.Any<IRoom>(), Arg.Any<IUser>(), Arg.Any<string>())
            .Returns(new List<LanguageModelMessage>());

        _command = new AskElsaCommand(
            _mockConfiguration,
            _mockResourcesService,
            _mockPersonalityService,
            _mockLanguageModelProvider,
            _mockTextToSpeechProvider,
            _mockConversationHistory
        );
    }
    
    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task RunAsync_ShouldReplyWithText_WhenAudioIsNotRequested()
    {
        // Arrange
        var mockContext = BuildContext("ask", "What is the weather?");
        _mockResourcesService.GetString("personality_prompt_silly", Arg.Any<CultureInfo>())
            .Returns("{0} asked by {1} in {2}");
        _mockLanguageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("It's sunny.");

        // Act
        await _command.RunAsync(mockContext);

        // Assert
        mockContext.Received(1).Reply("It's sunny.");
    }

    [Test]
    public async Task RunAsync_ShouldReplyWithAudio_WhenAudioIsRequested()
    {
        // Arrange
        var mockContext = BuildContext("askaudio", "What is the weather?");
        _mockResourcesService.GetString("personality_prompt_silly", Arg.Any<CultureInfo>())
            .Returns("{0} asked by {1} in {2}");
        _mockLanguageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("It's sunny.");
        _mockTextToSpeechProvider.GetTextToSpeechAudioUrlAsync(Arg.Any<string>(), Arg.Any<VoiceType>(), Arg.Any<CancellationToken>())
            .Returns("https://example.com/audio.mp3");

        // Act
        await _command.RunAsync(mockContext);

        // Assert
        mockContext.Received(1).ReplyHtml("""<audio src="https://example.com/audio.mp3" controls aria-label="It's sunny."></audio>""");
    }

    [Test]
    public async Task RunAsync_ShouldLogError_WhenAudioGenerationFails()
    {
        // Arrange
        var mockContext = BuildContext("askaudio", "What is the weather?");
        _mockResourcesService.GetString("personality_prompt_silly", Arg.Any<CultureInfo>())
            .Returns("{0} asked by {1} in {2}");
        _mockLanguageModelProvider.AskLanguageModelAsync(Arg.Any<LanguageModelRequest>(), Arg.Any<CancellationToken>())
            .Returns("It's sunny.");
        _mockTextToSpeechProvider.GetTextToSpeechAudioUrlAsync(Arg.Any<string>(), Arg.Any<VoiceType>(), Arg.Any<CancellationToken>())
            .Returns((string)null);

        // Act
        await _command.RunAsync(mockContext);

        // Assert
        mockContext.Received(1).ReplyLocalizedMessage("ask_error");
    }

    private static IContext BuildContext(string command, string target)
    {
        var context = Substitute.For<IContext>();
        var room = Substitute.For<IRoom>();
        var sender = Substitute.For<IUser>();

        room.Name.Returns("Room1");
        room.LastMessages.Returns(new List<Tuple<string, string>>());
        sender.Name.Returns("User");

        context.Command.Returns(command);
        context.Target.Returns(target);
        context.Room.Returns(room);
        context.Sender.Returns(sender);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        return context;
    }
}
