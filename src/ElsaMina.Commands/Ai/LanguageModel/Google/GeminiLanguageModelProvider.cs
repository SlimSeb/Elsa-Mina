using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Ai.LanguageModel.Google;

public abstract class GeminiLanguageModelProvider : ILanguageModelProvider
{
    private const string BASE_URL =
        "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

    private readonly IConfiguration _configuration;
    private readonly IHttpService _httpService;

    protected GeminiLanguageModelProvider(IConfiguration configuration, IHttpService httpService)
    {
        _configuration = configuration;
        _httpService = httpService;
    }
    
    protected abstract string Model { get; }
    private string Url => string.Format(BASE_URL, Model);

    public async Task<string> AskLanguageModelAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.GeminiApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            Log.Error("Missing Gemini API key");
            return null;
        }

        var headers = new Dictionary<string, string>
        {
            ["x-goog-api-key"] = apiKey
        };

        GeminiRequestDto requestDto = new();
        requestDto.Contents ??= [];
        requestDto.Contents.Add(new Content
        {
            Role = "user",
            Parts = [new ContentPart { Text = prompt }]
        });

        Log.Information("Making request to Gemini with prompt: {0}", prompt);
        var response = await _httpService.SendAsync<GeminiResponseDto>(
            HttpRequest.Post(BASE_URL).WithJsonBody(requestDto).WithHeaders(headers), cancellationToken);

        return response.Data?.Candidates?.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text ?? string.Empty;
    }

    public async Task<string> AskLanguageModelAsync(LanguageModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.GeminiApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            Log.Error("Missing Gemini API key");
            return null;
        }

        var headers = new Dictionary<string, string>
        {
            ["x-goog-api-key"] = apiKey
        };

        GeminiRequestDto requestDto = new();
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            requestDto.SystemInstruction = new SystemInstruction
            {
                Parts = [new InstructionPart { Text = request.SystemPrompt }]
            };
        }

        foreach (var message in request.InputConversation ?? [])
        {
            requestDto.Contents ??= [];
            var role = message.Role switch
            {
                MessageRole.Agent => "model",
                MessageRole.User => "user",
                _ => "user"
            };
            requestDto.Contents.Add(new Content
            {
                Role = role,
                Parts = [new ContentPart { Text = message.Content }]
            });
        }

        var response =
            await _httpService.SendAsync<GeminiResponseDto>(
                HttpRequest.Post(Url).WithJsonBody(requestDto).WithHeaders(headers), cancellationToken);

        return response.Data?.Candidates?.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text ?? string.Empty;
    }
}
