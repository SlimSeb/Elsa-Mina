using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Ai.LanguageModel.Mistral;

public abstract class MistralLanguageModelProvider : ILanguageModelProvider
{
    private const string MISTRAL_AUTOCOMPLETE_API_URL = "https://api.mistral.ai/v1/chat/completions";
    private const string USER_ROLE = "user";
    private const string ASSISTANT_ROLE = "assistant";
    private const string SYSTEM_ROLE = "system";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    protected MistralLanguageModelProvider(IHttpService httpService, IConfiguration configuration)
    {
        _httpService = httpService;
        _configuration = configuration;
    }
    
    protected abstract string Model { get; }

    public async Task<string> AskLanguageModelAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var key = _configuration.MistralApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            Log.Error("Missing Mistral API key");
            return null;
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {key}"
        };

        var dto = new MistralRequestDto
        {
            Model = Model,
            Messages =
            [
                new MistralRequestMessageDto
                {
                    Role = USER_ROLE,
                    Content = prompt
                }
            ]
        };

        Log.Information("Making request to Mistral with prompt: {0}", prompt);
        var response = await _httpService.SendAsync<MistralResponseDto>(
            HttpRequest.Post(MISTRAL_AUTOCOMPLETE_API_URL).WithJsonBody(dto).WithHeaders(headers),
            cancellationToken);

        var choice = response?.Data?.Choices?.FirstOrDefault();
        return choice?.Message.Content;
    }

    public async Task<string> AskLanguageModelAsync(LanguageModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = _configuration.MistralApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            Log.Error("Missing Mistral API key");
            return null;
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {key}"
        };

        var messages = new List<MistralRequestMessageDto>();
        if (!string.IsNullOrWhiteSpace(request?.SystemPrompt))
        {
            messages.Add(new MistralRequestMessageDto
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

                messages.Add(new MistralRequestMessageDto
                {
                    Role = role,
                    Content = msg.Content
                });
            }
        }

        var dto = new MistralRequestDto
        {
            Model = Model,
            Messages = messages
        };

        Log.Information("Making request to Mistral with full conversation context. Messages: {0}", messages.Count);
        var response = await _httpService.SendAsync<MistralResponseDto>(
            HttpRequest.Post(MISTRAL_AUTOCOMPLETE_API_URL).WithJsonBody(dto).WithHeaders(headers),
            cancellationToken);

        var choice = response?.Data?.Choices?.FirstOrDefault();
        return choice?.Message.Content;
    }
}
