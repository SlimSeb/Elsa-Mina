using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;

namespace ElsaMina.Commands.Alerts.Twitter;

public class TwitterApiClient : ITwitterApiClient
{
    private const string API_BASE_URL = "https://api.twitter.com/2";
    private const string MAX_RESULTS = "5";

    private static readonly Regex USERNAME_REGEX =
        new(@"^(?:(?:https?://)?(?:www\.|mobile\.)?(?:twitter|x)\.com/)?@?(\w{1,15})/?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase, Constants.REGEX_MATCH_TIMEOUT);

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    public TwitterApiClient(IHttpService httpService, IConfiguration configuration)
    {
        _httpService = httpService;
        _configuration = configuration;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration.TwitterBearerToken);

    public bool CanResolve(string platform) => platform == AlertPlatforms.TWITTER;

    public async Task<AlertChannel> ResolveChannelAsync(string input, CancellationToken cancellationToken = default)
    {
        var match = USERNAME_REGEX.Match(input ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        var username = Uri.EscapeDataString(match.Groups[1].Value);
        var response = await _httpService.SendAsync<TwitterUserResponse>(
            Authorize(HttpRequest.Get($"{API_BASE_URL}/users/by/username/{username}")), cancellationToken);
        var user = response.Data?.Data;
        return user == null ? null : new AlertChannel(user.Id, user.Username);
    }

    public async Task<TwitterTweetsResponse> GetLatestTweetsAsync(string userId, string sinceTweetId,
        CancellationToken cancellationToken = default)
    {
        var request = HttpRequest.Get($"{API_BASE_URL}/users/{Uri.EscapeDataString(userId)}/tweets")
            .WithQueryParameter("max_results", MAX_RESULTS)
            .WithQueryParameter("exclude", "replies,retweets");
        if (!string.IsNullOrEmpty(sinceTweetId))
        {
            request.WithQueryParameter("since_id", sinceTweetId);
        }

        var response = await _httpService.SendAsync<TwitterTweetsResponse>(Authorize(request), cancellationToken);
        return response.Data;
    }

    private HttpRequest Authorize(HttpRequest request)
    {
        return request.WithHeader("Authorization", $"Bearer {_configuration.TwitterBearerToken}");
    }
}
