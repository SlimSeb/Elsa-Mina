using System.Net;
using System.Text;
using System.Web;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.Dailymotion;

[NamedCommand("dailymotion", "daily")]
public class DailymotionCommand : Command
{
    private const string BASE_API_URI = "https://api.dailymotion.com/videos";

    private readonly IHttpService _httpService;
    private readonly ITemplatesManager _templatesManager;

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "dailymotion_help";

    public DailymotionCommand(IHttpService httpService, ITemplatesManager templatesManager)
    {
        _httpService = httpService;
        _templatesManager = templatesManager;
    }

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Target))
        {
            context.ReplyLocalizedMessage("dailymotion_no_video_found");
            return;
        }
        
        var keywords = string.Join('+', context.Target.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var queryParams = new Dictionary<string, string>
        {
            ["search"] = keywords,
            ["fields"] = "id,thumbnail_url,title,views_total,likes_total,explicit",
            ["limit"] = "5"
        };

        try
        {
            var result =
                await _httpService.SendAsync<VideoListResponse>(
                    HttpRequest.Get(BASE_API_URI).WithQueryParameters(queryParams), cancellationToken);
            if (result.StatusCode != HttpStatusCode.OK || result.Data == null || result.Data.List.Count == 0)
            {
                context.ReplyLocalizedMessage("dailymotion_no_video_found");
                return;
            }

            var video = result.Data.List
                .OrderByDescending(video => video.ViewsTotal)
                .FirstOrDefault(video => !video.Explicit);
            if (video == null)
            {
                context.ReplyLocalizedMessage("dailymotion_no_video_found");
                return;
            }

            var videoUrl = $"https://www.dailymotion.com/video/{HttpUtility.UrlEncode(video.Id)}";
            var template = await _templatesManager.GetTemplateAsync("Misc/Dailymotion/DailymotionVideoPreview",
                new DailymotionVideoPreviewViewModel
                {
                    Culture = context.Culture,
                    Title = video.Title,
                    VideoUrl = videoUrl,
                    ThumbnailUrl = video.ThumbnailUrl,
                    ViewsTotal = video.ViewsTotal,
                    LikesTotal = video.LikesTotal
                });
            context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred fetching Dailymotion video");
            context.ReplyRankAwareLocalizedMessage("dailymotion_fetch_error", exception.Message);
        }
    }
}