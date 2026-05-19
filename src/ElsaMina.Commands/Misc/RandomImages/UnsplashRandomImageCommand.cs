using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.RandomImages;

public abstract class UnsplashRandomImageCommand : Command
{
    private const int MAX_WIDTH = 300;
    private const int MAX_HEIGHT = 200;
    
    protected abstract string Query { get; }
    protected virtual string WarningKey => null;

    private readonly IUnsplashService _unsplashService;
    private readonly IImageService _imageService;

    protected UnsplashRandomImageCommand(IUnsplashService unsplashService, IImageService imageService)
    {
        _unsplashService = unsplashService;
        _imageService = imageService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "rand_unsplash_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var photoUrl = await _unsplashService.GetRandomPhotoUrlAsync(Query, cancellationToken);
        if (string.IsNullOrEmpty(photoUrl))
        {
            Log.Error("Unsplash returned no photo for query: {Query}", Query);
            context.ReplyLocalizedMessage("random_image_error");
            return;
        }

        var (width, height) = await _imageService.GetRemoteImageDimensions(photoUrl, cancellationToken);
        if (width < 0 || height < 0)
        {
            width = MAX_WIDTH;
            height = MAX_HEIGHT;
        }
        else
        {
            (width, height) = ImageUtils.ResizeWithSameAspectRatio(width, height, MAX_WIDTH, MAX_HEIGHT);
        }
        var img = $"<img src=\"{photoUrl}\" style=\"transform:rotate(0deg);\" width=\"{width}\" height=\"{height}\">";
        var html = WarningKey != null
            ? $"<details><summary>{context.GetString(WarningKey)}</summary>{img}</details>"
            : img;

        context.ReplyHtml(html, rankAware: true);
    }
}
