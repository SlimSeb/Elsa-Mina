using ElsaMina.Core.Services.Http;

namespace ElsaMina.Commands.Showdown.SmogonStats;

public class SmogonUsageDataProvider : ISmogonUsageDataProvider
{
    private const string USAGE_DATA_URL = "https://www.smogon.com/stats/{0}/chaos/{1}-{2}.json";

    private readonly IHttpService _httpService;

    public SmogonUsageDataProvider(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public async Task<SmogonUsageDataDto> GetUsageDataAsync(string month, string format, int playerLevel,
        CancellationToken cancellationToken = default)
    {
        var url = string.Format(USAGE_DATA_URL, month, format, playerLevel);
        var response = await _httpService.SendAsync<SmogonUsageDataDto>(HttpRequest.Get(url), cancellationToken);
        return response.Data;
    }
}
