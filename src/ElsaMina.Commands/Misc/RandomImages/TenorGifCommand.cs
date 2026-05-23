using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Misc.RandomImages;

[NamedCommand("tenorgif")]
public class TenorGifCommand : Command
{
    private const string TENOR_CDN_HOST = "media.tenor.com";

    private readonly IImageService _imageService;
    private readonly ITemplatesManager _templatesManager;

    public TenorGifCommand(IImageService imageService, ITemplatesManager templatesManager)
    {
        _imageService = imageService;
        _templatesManager = templatesManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "tenorgif_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var isEnabled = (await context.Room.GetParameterValueAsync(Parameter.TenorGifEnabled,
            cancellationToken)).ToBoolean();
        if (!isEnabled)
        {
            return;
        }

        var target = context.Target?.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var separatorIndex = target.LastIndexOf('|');
        string url;
        int width = 0, height = 0;

        if (separatorIndex > 0)
        {
            var dimensionPart = target[(separatorIndex + 1)..];
            var urlAndWidth = target[..separatorIndex];
            var widthSeparator = urlAndWidth.LastIndexOf('|');

            if (widthSeparator > 0
                && int.TryParse(urlAndWidth[(widthSeparator + 1)..], out width)
                && int.TryParse(dimensionPart, out height))
            {
                url = urlAndWidth[..widthSeparator];
            }
            else
            {
                url = target;
            }
        }
        else
        {
            url = target;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Host.Equals(TENOR_CDN_HOST, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme != "https")
        {
            context.ReplyLocalizedMessage("tenorgif_invalid_url");
            return;
        }

        if (width <= 0 || height <= 0)
        {
            (width, height) = await _imageService.GetRemoteImageDimensions(url, cancellationToken);
        }

        var template = await _templatesManager.GetTemplateAsync("Misc/RandomImages/TenorGif",
            new TenorGifViewModel
            {
                Culture = context.Culture,
                Url = url,
                Width = width / 2,
                Height = height / 2
            });

        context.ReplyHtml(template.RemoveNewlines());
    }
}
