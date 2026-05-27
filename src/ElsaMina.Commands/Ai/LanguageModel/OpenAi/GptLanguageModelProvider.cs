using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Ai.LanguageModel.OpenAi;

public abstract class GptLanguageModelProvider : ILanguageModelProvider
{
    private const string OPENAI_CONVERSATIONS_URL = "https://api.openai.com/v1/chat/completions";
    private const string SYSTEM_ROLE = "system";
    private const string USER_ROLE = "user";
    private const string ASSISTANT_ROLE = "assistant";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    protected GptLanguageModelProvider(IHttpService httpService, IConfiguration configuration)
    {
        _httpService = httpService;
        _configuration = configuration;
    }

    protected abstract string Model { get; }

    public async Task<string> AskLanguageModelAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.ChatGptApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Missing ChatGPT API key");
            return null;
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {apiKey}"
        };

        var dto = new GptRequestDto
        {
            Model = Model,
            Messages = [
                new GptConversationItemDto
                {
                    Role = USER_ROLE,
                    Content = prompt
                }
            ]
        };

        Log.Information("Making request to OpenAI's GPT with prompt: {0}", prompt);
        var response = await _httpService.SendAsync<GptResponseDto>(
            HttpRequest.Post(OPENAI_CONVERSATIONS_URL).WithJsonBody(dto).WithHeaders(headers),
            cancellationToken);

        var assistantMessage = response?.Data?.Items?
            .LastOrDefault(item => string.Equals(item.Role, ASSISTANT_ROLE, StringComparison.OrdinalIgnoreCase));
        return assistantMessage?.Content;
    }

    public async Task<string> AskLanguageModelAsync(LanguageModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.ChatGptApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Missing ChatGPT API key");
            return null;
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {apiKey}"
        };

        var items = new List<GptConversationItemDto>();
        if (!string.IsNullOrWhiteSpace(request?.SystemPrompt))
        {
            items.Add(new GptConversationItemDto
            {
                Role = SYSTEM_ROLE,
                Content = request.SystemPrompt
            });
        }

        if (request?.InputConversation != null)
        {
            foreach (var msg in request.InputConversation)
            {
                if (msg == null)
                {
                    continue;
                }

                var role = msg.Role switch
                {
                    MessageRole.Agent => ASSISTANT_ROLE,
                    MessageRole.User => USER_ROLE,
                    _ => USER_ROLE
                };

                items.Add(new GptConversationItemDto
                {
                    Role = role,
                    Content = msg.Content
                });
            }
        }

        var dto = new GptRequestDto
        {
            Model =  Model,
            Messages = items
        };

        var response = await _httpService.SendAsync<GptResponseDto>(
            HttpRequest.Post(OPENAI_CONVERSATIONS_URL).WithJsonBody(dto).WithHeaders(headers),
            cancellationToken);

        var assistantMessage = response?.Data?.Items?
            .LastOrDefault(item => string.Equals(item.Role, ASSISTANT_ROLE, StringComparison.OrdinalIgnoreCase));
        return assistantMessage?.Content;
    }
}
