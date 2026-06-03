using System.Net;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.DataAccess;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Games.Semantix;

/// <summary>
/// OpenAI embeddings provider, kept as a fallback option. Not registered in DI:
/// the game runs on a single provider (currently Gemini) because vectors from
/// different models are not comparable with each other.
/// </summary>
public class OpenAiEmbeddingService : EmbeddingServiceBase
{
    private const string MODEL = "text-embedding-3-small";
    private const string EMBEDDINGS_URL = "https://api.openai.com/v1/embeddings";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    public OpenAiEmbeddingService(IHttpService httpService,
        IConfiguration configuration,
        IBotDbContextFactory dbContextFactory,
        IClockService clockService) : base(dbContextFactory, clockService)
    {
        _httpService = httpService;
        _configuration = configuration;
    }

    protected override string ModelName => MODEL;

    protected override async Task<float[]> FetchFromApiAsync(string word, CancellationToken cancellationToken)
    {
        var apiKey = _configuration.ChatGptApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Missing ChatGPT API key for Semantix embeddings");
            return null;
        }

        try
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {apiKey}"
            };

            var dto = new EmbeddingRequestDto
            {
                Model = MODEL,
                Input = word,
                Dimensions = SemantixConstants.EMBEDDING_DIMENSIONS
            };

            var response = await _httpService.SendAsync<EmbeddingResponseDto>(
                HttpRequest.Post(EMBEDDINGS_URL).WithJsonBody(dto).WithHeaders(headers),
                cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                Log.Error("OpenAI embeddings API returned status code {0} for word {1}", response.StatusCode, word);
                return null;
            }

            return response.Data?.Data?.FirstOrDefault()?.Embedding;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "OpenAI embeddings API call failed for word {0}", word);
            return null;
        }
    }
}
