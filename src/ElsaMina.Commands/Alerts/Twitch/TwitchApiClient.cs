using System.Net;
using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;

namespace ElsaMina.Commands.Alerts.Twitch;

public class TwitchApiClient : ITwitchApiClient
{
    private const string TOKEN_URL = "https://id.twitch.tv/oauth2/token";
    private const string USERS_URL = "https://api.twitch.tv/helix/users";
    private const string STREAMS_URL = "https://api.twitch.tv/helix/streams";
    private const int MAX_IDS_PER_REQUEST = 100;
    private static readonly TimeSpan TOKEN_EXPIRY_MARGIN = TimeSpan.FromMinutes(5);

    private static readonly Regex LOGIN_REGEX = new(@"^(?:https?://)?(?:www\.)?(?:twitch\.tv/)?@?(\w{1,25})/?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase, Constants.REGEX_MATCH_TIMEOUT);

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;
    private readonly IClockService _clockService;
    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);

    private string _accessToken;
    private DateTimeOffset _accessTokenExpiry;

    public TwitchApiClient(IHttpService httpService, IConfiguration configuration, IClockService clockService)
    {
        _httpService = httpService;
        _configuration = configuration;
        _clockService = clockService;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration.TwitchClientId)
                                && !string.IsNullOrWhiteSpace(_configuration.TwitchClientSecret);

    public bool CanResolve(string platform) => platform == AlertPlatforms.TWITCH;

    public async Task<AlertChannel> ResolveChannelAsync(string input, CancellationToken cancellationToken = default)
    {
        var match = LOGIN_REGEX.Match(input ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        var login = match.Groups[1].Value.ToLowerInvariant();
        var response = await SendAuthorizedAsync<TwitchUsersResponse>(
            () => HttpRequest.Get(USERS_URL).WithQueryParameter("login", login), cancellationToken);
        var user = response?.Data?.FirstOrDefault();
        return user == null ? null : new AlertChannel(user.Id, user.Login);
    }

    public async Task<IReadOnlyList<TwitchStream>> GetLiveStreamsAsync(IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var streams = new List<TwitchStream>();
        foreach (var chunk in userIds.Chunk(MAX_IDS_PER_REQUEST))
        {
            // Helix attend le paramètre user_id répété, ce que HttpRequest ne sait pas exprimer
            var query = string.Join("&", chunk.Select(userId => $"user_id={Uri.EscapeDataString(userId)}"));
            var response = await SendAuthorizedAsync<TwitchStreamsResponse>(
                () => HttpRequest.Get($"{STREAMS_URL}?{query}&first={MAX_IDS_PER_REQUEST}"), cancellationToken);
            if (response?.Data != null)
            {
                streams.AddRange(response.Data);
            }
        }

        return streams;
    }

    private async Task<TResponse> SendAuthorizedAsync<TResponse>(Func<HttpRequest> requestBuilder,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendWithTokenAsync<TResponse>(requestBuilder(), cancellationToken);
        }
        catch (HttpException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Le token a pu être révoqué avant son expiration : on en redemande un et on réessaie une fois
            _accessToken = null;
            return await SendWithTokenAsync<TResponse>(requestBuilder(), cancellationToken);
        }
    }

    private async Task<TResponse> SendWithTokenAsync<TResponse>(HttpRequest request,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        request
            .WithHeader("Client-Id", _configuration.TwitchClientId)
            .WithHeader("Authorization", $"Bearer {token}");
        var response = await _httpService.SendAsync<TResponse>(request, cancellationToken);
        return response.Data;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _tokenSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken != null && _clockService.CurrentUtcDateTimeOffset < _accessTokenExpiry)
            {
                return _accessToken;
            }

            var request = HttpRequest.Post(TOKEN_URL).WithFormBody(new Dictionary<string, string>
            {
                ["client_id"] = _configuration.TwitchClientId,
                ["client_secret"] = _configuration.TwitchClientSecret,
                ["grant_type"] = "client_credentials"
            });
            var response = await _httpService.SendAsync<TwitchTokenResponse>(request, cancellationToken);
            _accessToken = response.Data?.AccessToken
                           ?? throw new InvalidOperationException("Twitch did not return an access token.");
            _accessTokenExpiry = _clockService.CurrentUtcDateTimeOffset
                                 + TimeSpan.FromSeconds(response.Data.ExpiresIn) - TOKEN_EXPIRY_MARGIN;
            return _accessToken;
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }
}
