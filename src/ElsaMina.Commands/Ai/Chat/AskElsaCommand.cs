using ElsaMina.Commands.Ai.LanguageModel;
using ElsaMina.Commands.Ai.TextToSpeech;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Ai.Chat;

[NamedCommand("ask-elsa", "ask", "ai", "aichat", "ai-chat", "askaudio", "askelsaaudio", "ask-elsa-audio",
    "ai-chat-audio", "aiaudio", "ai-audio")]
public class AskElsaCommand : Command
{
    private readonly IConversationHistoryService _conversationHistory;
    private readonly IConfiguration _configuration;
    private readonly IResourcesService _resourcesService;
    private readonly IPersonalityService _personalityService;
    private readonly ILanguageModelProvider _languageModelProvider;
    private readonly IAiTextToSpeechProvider _textToSpeechProvider;

    public AskElsaCommand(IConfiguration configuration,
        IResourcesService resourcesService,
        IPersonalityService personalityService,
        ILanguageModelProvider languageModelProvider,
        IAiTextToSpeechProvider textToSpeechProvider,
        IConversationHistoryService conversationHistory)
    {
        _configuration = configuration;
        _resourcesService = resourcesService;
        _personalityService = personalityService;
        _languageModelProvider = languageModelProvider;
        _textToSpeechProvider = textToSpeechProvider;
        _conversationHistory = conversationHistory;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "ask_elsa_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var withAudio = context.Command.Contains("audio");
        var room = context.Room;
        var personality = _personalityService.GetPersonality(context.RoomId);
        var systemPrompt = string.Format(
            _resourcesService.GetString(BotPersonalities.GetPromptKey(personality), context.Culture),
            context.Sender.Name,
            _configuration.Name,
            room.Name);

        var conversation = _conversationHistory.BuildConversation(room, context.Sender, context.Target);
        var request = new LanguageModelRequest
        {
            SystemPrompt = systemPrompt,
            InputConversation = conversation
        };

        var response = await _languageModelProvider.AskLanguageModelAsync(request, cancellationToken);
        if (response == null)
        {
            context.ReplyLocalizedMessage("ask_error");
            return;
        }

        if (withAudio)
        {
            var url = await _textToSpeechProvider.GetTextToSpeechAudioUrlAsync(response, VoiceType.Female,
                cancellationToken);
            if (string.IsNullOrEmpty(url))
            {
                context.ReplyLocalizedMessage("ask_error");
                Log.Error("Failed to upload audio file");
                return;
            }

            context.ReplyHtml($"""<audio src="{url}" controls aria-label="{response}"></audio>""");
            return;
        }

        context.Reply(response);
    }
}
