using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.RandomImages;

public class UnsplashService : IUnsplashService
{
    private const string UNSPLASH_RANDOM_URL = "https://api.unsplash.com/photos/random";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    public UnsplashService(IHttpService httpService, IConfiguration configuration)
    {
        _httpService = httpService;
        _configuration = configuration;
    }

    public async Task<string> GetRandomPhotoUrlAsync(string query, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.UnsplashApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Unsplash API key is empty.");
            return null;
        }

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = apiKey,
            ["query"] = query
        };

        try
        {
            var response = await _httpService.SendAsync<UnsplashPhotoDto>(
                HttpRequest.Get(UNSPLASH_RANDOM_URL).WithQueryParameters(queryParams), cancellationToken);
            return response.Data?.Urls?.Regular;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch Unsplash photo for query: {Query}", query);
            return null;
        }
    }
}
