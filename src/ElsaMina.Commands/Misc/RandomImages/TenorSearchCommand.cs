using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("tenorsearch")]
public class TenorSearchCommand : Command
{
    private const int GIF_COUNT = 8;
    private const int THUMBNAIL_MAX_WIDTH = 150;

    private readonly ITenorService _tenorService;
    private readonly IConfiguration _configuration;
    private readonly ITemplatesManager _templatesManager;

    public TenorSearchCommand(ITenorService tenorService, IConfiguration configuration,
        ITemplatesManager templatesManager)
    {
        _tenorService = tenorService;
        _configuration = configuration;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => false;
    public override string HelpMessageKey => "tenorsearch_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var gifs = await _tenorService.GetMultipleMediaAsync(
            context.Target.Trim(), "gif", GIF_COUNT, cancellationToken);

        if (gifs == null || gifs.Count == 0)
        {
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        var thumbnails = gifs.Select(gif =>
        {
            var thumbWidth = Math.Min(gif.Width / 2, THUMBNAIL_MAX_WIDTH);
            var thumbHeight = gif.Width > 0 ? gif.Height * thumbWidth / gif.Width : thumbWidth;
            return new TenorGifThumbnail
            {
                Url = gif.Url,
                OriginalWidth = gif.Width,
                OriginalHeight = gif.Height,
                ThumbWidth = thumbWidth,
                ThumbHeight = thumbHeight
            };
        }).ToList();

        var template = await _templatesManager.GetTemplateAsync("Misc/RandomImages/TenorSearch",
            new TenorSearchViewModel
            {
                Culture = context.Culture,
                Gifs = thumbnails,
                Trigger = _configuration.Trigger
            });

        context.SendHtmlTo(context.Sender.UserId, template.RemoveNewlines());
    }
}
