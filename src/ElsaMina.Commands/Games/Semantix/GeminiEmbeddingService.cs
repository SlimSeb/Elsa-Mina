using System.Net;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.DataAccess;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Games.Semantix;

public class GeminiEmbeddingService : EmbeddingServiceBase
{
    private const string MODEL = "gemini-embedding-001";

    private const string EMBEDDINGS_URL =
        $"https://generativelanguage.googleapis.com/v1beta/models/{MODEL}:embedContent";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    public GeminiEmbeddingService(IHttpService httpService,
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
        var apiKey = _configuration.GeminiApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Missing Gemini API key for Semantix embeddings");
            return null;
        }

        try
        {
            var headers = new Dictionary<string, string>
            {
                ["x-goog-api-key"] = apiKey
            };

            var dto = new GeminiEmbeddingRequestDto
            {
                Model = $"models/{MODEL}",
                Content = new GeminiEmbeddingContentDto
                {
                    Parts = [new GeminiEmbeddingPartDto { Text = word }]
                },
                OutputDimensionality = SemantixConstants.EMBEDDING_DIMENSIONS
            };

            var response = await _httpService.SendAsync<GeminiEmbeddingResponseDto>(
                HttpRequest.Post(EMBEDDINGS_URL).WithJsonBody(dto).WithHeaders(headers),
                cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                Log.Error("Gemini embeddings API returned status code {0} for word {1}", response.StatusCode, word);
                return null;
            }

            return response.Data?.Embedding?.Values;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Gemini embeddings API call failed for word {0}", word);
            return null;
        }
    }
}
