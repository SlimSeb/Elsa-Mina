using System.Net;
using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;
using HtmlAgilityPack;

namespace ElsaMina.Commands.Misc.UrlPreview;

public class UrlPreviewHandler : ChatMessageHandler
{
    private const int MAX_WIDTH = 80;
    private const int MAX_HEIGHT = 80;

    private static readonly Regex URL_REGEX =
        new(@"https?://[^\s<>""]+", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private static readonly Regex YOUTUBE_URL_REGEX =
        new(@"(?:https?://)?(?:(?:www\.|m\.)?youtube\.com/(?:watch\?(?:.*&)?v=|shorts/)|youtu\.be/)[A-Za-z0-9_-]{11}",
            RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private static readonly Regex REPLAY_URL_REGEX =
        new(@"https://replay\.pokemonshowdown\.com/", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private const int MAX_DESCRIPTION_LENGTH = 300;

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;
    private readonly ITemplatesManager _templatesManager;
    private readonly IImageService _imageService;

    public UrlPreviewHandler(IContextFactory contextFactory,
        IHttpService httpService,
        IConfiguration configuration,
        ITemplatesManager templatesManager,
        IImageService imageService) : base(contextFactory)
    {
        _httpService = httpService;
        _configuration = configuration;
        _templatesManager = templatesManager;
        _imageService = imageService;
    }

    public override async Task HandleMessageAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var isPreviewEnabled = (await context.Room.GetParameterValueAsync(Parameter.ShowUrlPreview,
            cancellationToken)).ToBoolean();
        if (!isPreviewEnabled)
        {
            return;
        }

        if (context.Sender.UserId == _configuration.Name.ToLowerAlphaNum()
            || context.Message.StartsWith(_configuration.Trigger)
            || context.Message.StartsWith("/raw")
            || context.Message.StartsWith("!show"))
        {
            return;
        }

        var urlMatch = URL_REGEX.Match(context.Message);
        if (!urlMatch.Success)
        {
            return;
        }

        var url = urlMatch.Value.TrimEnd('.', ',', ')', ']');

        if (YOUTUBE_URL_REGEX.IsMatch(url) || REPLAY_URL_REGEX.IsMatch(url))
        {
            return;
        }

        try
        {
            var response = await _httpService.GetAsync<string>(url, isRaw: true,
                headers: new Dictionary<string, string>
                {
                    ["Accept"] = "text/html",
                    ["User-Agent"] = "Mozilla/5.0 (compatible; bot)"
                },
                cancellationToken: cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Data))
            {
                return;
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response.Data);

            var ogData = ParseOpenGraph(htmlDoc);
            if (!ogData.TryGetValue("title", out var title) || string.IsNullOrWhiteSpace(title))
            {
                var titleNode = htmlDoc.DocumentNode.SelectSingleNode("//title");
                if (titleNode == null)
                {
                    return;
                }

                title = HtmlEntity.DeEntitize(titleNode.InnerText)?.Trim() ?? string.Empty;
            }

            ogData.TryGetValue("description", out var description);
            ogData.TryGetValue("image", out var image);
            ogData.TryGetValue("site_name", out var siteName);

            int width = -1;
            int height = -1;
            if (!string.IsNullOrEmpty(image))
            {
                (width, height) = await _imageService.GetRemoteImageDimensions(image, cancellationToken);
                if (width <= 0 || height <= 0)
                {
                    width = MAX_WIDTH;
                    height = MAX_HEIGHT;
                }
                else
                {
                    (width, height) = ImageUtils.ResizeWithSameAspectRatio(width, height, MAX_WIDTH, MAX_HEIGHT);
                }
            }

            var template = await _templatesManager.GetTemplateAsync("Misc/UrlPreview/UrlPreview",
                new UrlPreviewViewModel
                {
                    Culture = context.Culture,
                    Url = url,
                    Title = title,
                    Description = description?.Shorten(MAX_DESCRIPTION_LENGTH),
                    ImageUrl = image,
                    ImageWidth = width,
                    ImageHeight = height,
                    SiteName = siteName
                });

            context.ReplyHtml(template.RemoveNewlines());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch URL preview for {0}", url);
        }
    }

    private static Dictionary<string, string> ParseOpenGraph(HtmlDocument htmlDoc)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var metaNodes = htmlDoc.DocumentNode.SelectNodes("//meta[@property and @content]");
        if (metaNodes == null)
        {
            return result;
        }

        foreach (var node in metaNodes)
        {
            var property = node.GetAttributeValue("property", string.Empty);
            var content = node.GetAttributeValue("content", string.Empty);
            if (property.StartsWith("og:", StringComparison.OrdinalIgnoreCase))
            {
                var key = property["og:".Length..];
                result.TryAdd(key, HtmlEntity.DeEntitize(content));
            }
        }

        return result;
    }
}