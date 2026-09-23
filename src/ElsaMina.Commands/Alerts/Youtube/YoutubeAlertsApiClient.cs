using System.Text.RegularExpressions;
using System.Xml.Linq;
using ElsaMina.Core;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;

namespace ElsaMina.Commands.Alerts.Youtube;

public class YoutubeAlertsApiClient : IYoutubeAlertsApiClient
{
    private const string CHANNELS_URL = "https://www.googleapis.com/youtube/v3/channels";
    private const string VIDEOS_URL = "https://www.googleapis.com/youtube/v3/videos";
    private const string FEED_URL = "https://www.youtube.com/feeds/videos.xml";
    private const int MAX_IDS_PER_REQUEST = 50;

    private static readonly XNamespace ATOM_NAMESPACE = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace YOUTUBE_NAMESPACE = "http://www.youtube.com/xml/schemas/2015";

    private static readonly Regex CHANNEL_ID_REGEX =
        new(@"^(?:(?:https?://)?(?:www\.|m\.)?youtube\.com/channel/)?(UC[\w-]{22})/?$",
            RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private static readonly Regex HANDLE_REGEX =
        new(@"^(?:(?:https?://)?(?:www\.|m\.)?youtube\.com/)?@?([\w.-]{3,30})/?$",
            RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    public YoutubeAlertsApiClient(IHttpService httpService, IConfiguration configuration)
    {
        _httpService = httpService;
        _configuration = configuration;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration.YoutubeApiKey);

    public bool CanResolve(string platform) =>
        platform is AlertPlatforms.YOUTUBE or AlertPlatforms.YOUTUBE_LIVE;

    public async Task<AlertChannel> ResolveChannelAsync(string input, CancellationToken cancellationToken = default)
    {
        input ??= string.Empty;
        var request = HttpRequest.Get(CHANNELS_URL)
            .WithQueryParameter("part", "snippet")
            .WithQueryParameter("key", _configuration.YoutubeApiKey);

        var channelIdMatch = CHANNEL_ID_REGEX.Match(input);
        var handleMatch = HANDLE_REGEX.Match(input);
        if (channelIdMatch.Success)
        {
            request.WithQueryParameter("id", channelIdMatch.Groups[1].Value);
        }
        else if (handleMatch.Success)
        {
            request.WithQueryParameter("forHandle", $"@{handleMatch.Groups[1].Value}");
        }
        else
        {
            return null;
        }

        var response = await _httpService.SendAsync<YoutubeChannelsResponse>(request, cancellationToken);
        var channel = response.Data?.Items?.FirstOrDefault();
        if (channel == null)
        {
            return null;
        }

        var handle = channel.Snippet?.CustomUrl?.TrimStart('@');
        var channelName = string.IsNullOrWhiteSpace(handle) ? channel.Snippet?.Title ?? channel.Id : handle;
        return new AlertChannel(channel.Id, channelName);
    }

    public async Task<IReadOnlyList<string>> GetRecentVideoIdsAsync(string channelId,
        CancellationToken cancellationToken = default)
    {
        // Le flux RSS de la chaîne ne consomme pas de quota de l'API YouTube
        var response = await _httpService.SendForStringAsync(
            HttpRequest.Get(FEED_URL).WithQueryParameter("channel_id", channelId), cancellationToken);
        var document = XDocument.Parse(response.Data ?? string.Empty);
        return document.Root?
            .Elements(ATOM_NAMESPACE + "entry")
            .Select(entry => entry.Element(YOUTUBE_NAMESPACE + "videoId")?.Value)
            .Where(videoId => !string.IsNullOrEmpty(videoId))
            .ToList() ?? [];
    }

    public async Task<IReadOnlyList<YoutubeVideo>> GetVideosAsync(IReadOnlyCollection<string> videoIds,
        CancellationToken cancellationToken = default)
    {
        var videos = new List<YoutubeVideo>();
        foreach (var chunk in videoIds.Chunk(MAX_IDS_PER_REQUEST))
        {
            var request = HttpRequest.Get(VIDEOS_URL)
                .WithQueryParameter("part", "snippet,liveStreamingDetails")
                .WithQueryParameter("id", string.Join(",", chunk))
                .WithQueryParameter("key", _configuration.YoutubeApiKey);
            var response = await _httpService.SendAsync<YoutubeVideosResponse>(request, cancellationToken);
            if (response.Data?.Items != null)
            {
                videos.AddRange(response.Data.Items);
            }
        }

        return videos;
    }
}
