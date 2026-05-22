using System.Globalization;
using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.Youtube;

public class YoutubeVideoOnLinkHandler : ChatMessageHandler
{
    private static readonly Regex YOUTUBE_URL_REGEX =
        new(@"(?:https?://)?(?:(?:www\.|m\.)?youtube\.com/(?:watch\?(?:[^&]*&)?v=|shorts/)|youtu\.be/)([A-Za-z0-9_-]{11})",
            RegexOptions.Compiled | RegexOptions.NonBacktracking, Constants.REGEX_MATCH_TIMEOUT);

    private const string YOUTUBE_VIDEOS_API_URL = "https://www.googleapis.com/youtube/v3/videos";
    private const int THUMBNAIL_WIDTH = 160;
    private const int THUMBNAIL_HEIGHT = 90;

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;
    private readonly ITemplatesManager _templatesManager;

    public YoutubeVideoOnLinkHandler(IContextFactory contextFactory,
        IHttpService httpService,
        IConfiguration configuration,
        ITemplatesManager templatesManager) : base(contextFactory)
    {
        _httpService = httpService;
        _configuration = configuration;
        _templatesManager = templatesManager;
    }

    public override async Task HandleMessageAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var isPreviewEnabled = (await context.Room.GetParameterValueAsync(Parameter.ShowYoutubeLinkPreview,
            cancellationToken)).ToBoolean();
        if (!isPreviewEnabled)
        {
            return;
        }

        if (context.Sender.UserId == _configuration.Name.ToLowerAlphaNum())
        {
            return;
        }

        if (context.Message.StartsWith("!show"))
        {
            return;
        }

        var match = YOUTUBE_URL_REGEX.Match(context.Message);
        if (!match.Success)
        {
            return;
        }

        var videoId = match.Groups[1].Value;
        var apiKey = _configuration.YoutubeApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("YouTube API key is empty, cannot fetch video info for link preview.");
            return;
        }

        try
        {
            var queryParams = new Dictionary<string, string>
            {
                ["part"] = "snippet",
                ["id"] = videoId,
                ["key"] = apiKey
            };

            var response = await _httpService.GetAsync<YouTubeVideoListResponse>(YOUTUBE_VIDEOS_API_URL, queryParams,
                cancellationToken: cancellationToken);
            var data = response.Data;

            if (data?.Items == null || data.Items.Count == 0)
            {
                return;
            }

            var snippet = data.Items[0].Snippet;
            var publishedAt = DateTime.ParseExact(
                snippet.PublishedAt,
                "yyyy-MM-ddTHH:mm:ssZ",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind
            );
            var thumbnail = snippet.Thumbnails?.Medium ?? snippet.Thumbnails?.Default;

            var template = await _templatesManager.GetTemplateAsync("Misc/Youtube/YoutubeVideoPreview",
                new YoutubeVideoPreviewViewModel
                {
                    Culture = context.Culture,
                    VideoId = videoId,
                    Title = snippet.Title,
                    Description = snippet.Description ?? string.Empty,
                    ChannelTitle = snippet.ChannelTitle,
                    PublishTime = publishedAt,
                    ThumbnailSource = thumbnail?.Url ?? string.Empty,
                    ThumbnailWidth = THUMBNAIL_WIDTH,
                    ThumbnailHeight = THUMBNAIL_HEIGHT
                });

            context.ReplyHtml(template.RemoveNewlines());
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to fetch YouTube video info for video {0}", videoId);
        }
    }
}
