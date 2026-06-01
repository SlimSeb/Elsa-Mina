using System.Net;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.BugReport;

public class GithubIssueService : IGithubIssueService
{
    private const string GITHUB_API_BASE_URL = "https://api.github.com/repos";
    private const string USER_AGENT = "Elsa-Mina";
    private const string API_VERSION = "2022-11-28";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    public GithubIssueService(IHttpService httpService, IConfiguration configuration)
    {
        _httpService = httpService;
        _configuration = configuration;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration.GithubToken) &&
        !string.IsNullOrWhiteSpace(_configuration.GithubRepository);

    public async Task<GithubIssueResponseDto> CreateIssueAsync(string title, string body,
        IEnumerable<string> labels = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            Log.Error("Github issue service is not configured.");
            return null;
        }

        var url = $"{GITHUB_API_BASE_URL}/{_configuration.GithubRepository}/issues";
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {_configuration.GithubToken}",
            ["Accept"] = "application/vnd.github+json",
            ["User-Agent"] = USER_AGENT,
            ["X-GitHub-Api-Version"] = API_VERSION
        };

        var dto = new GithubIssueRequestDto
        {
            Title = title,
            Body = body,
            Labels = labels
        };

        var response = await _httpService.SendAsync<GithubIssueResponseDto>(
            HttpRequest.Post(url).WithJsonBody(dto).WithHeaders(headers),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            return response.Data;
        }

        Log.Error("Failed to create Github issue, received status code {0}", response.StatusCode);
        return null;
    }
}
