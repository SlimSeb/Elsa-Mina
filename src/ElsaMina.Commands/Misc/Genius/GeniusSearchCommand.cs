using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.Genius;

[NamedCommand("genius", "lyrics", "song")]
public class GeniusSearchCommand : Command
{
    private const string GENIUS_API_URL = "https://api.genius.com/search";
    private const string SONG_TYPE = "song";
    private const int MAX_THUMBNAIL_WIDTH = 115;
    private const int MAX_THUMBNAIL_HEIGHT = 115;

    private readonly IConfiguration _configuration;
    private readonly IHttpService _httpService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IImageService _imageService;

    public GeniusSearchCommand(IConfiguration configuration, IHttpService httpService,
        ITemplatesManager templatesManager, IImageService imageService)
    {
        _configuration = configuration;
        _httpService = httpService;
        _templatesManager = templatesManager;
        _imageService = imageService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "genius_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var geniusApiKey = _configuration.GeniusApiKey;
        if (string.IsNullOrWhiteSpace(geniusApiKey))
        {
            Log.Error("Please specify a Genius API key in configuration");
            return;
        }

        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var parameters = new Dictionary<string, string>
        {
            ["q"] = context.Target,
            ["access_token"] = geniusApiKey
        };

        try
        {
            var response = await _httpService.SendAsync<GeniusSearchResult>(
                HttpRequest.Get(GENIUS_API_URL).WithQueryParameters(parameters), cancellationToken);

            var mostViewedHit = response.Data.Response.Hits
                .Where(hit => hit.Type == SONG_TYPE)
                .MaxBy(hit => hit.Result.Stats.Pageviews);

            if (mostViewedHit == null)
            {
                context.ReplyLocalizedMessage("genius_not_found");
                return;
            }

            var thumbnailUrl = mostViewedHit.Result.SongArtImageUrl;
            var (width, height) = await _imageService.GetRemoteImageDimensions(thumbnailUrl, cancellationToken);
            if (width < 0 || height < 0)
            {
                width = MAX_THUMBNAIL_WIDTH;
                height = MAX_THUMBNAIL_HEIGHT;
            }
            else
            {
                (width, height) = ImageUtils.ResizeWithSameAspectRatio(width, height,
                    MAX_THUMBNAIL_WIDTH, MAX_THUMBNAIL_HEIGHT);
            }
            DateOnly releaseDate = default;
            if (mostViewedHit.Result.ReleaseDateComponents is { Year: not null, Month: not null, Day: not null })
            {
                var year = mostViewedHit.Result.ReleaseDateComponents.Year.Value;
                var month = mostViewedHit.Result.ReleaseDateComponents.Month.Value;
                var day = mostViewedHit.Result.ReleaseDateComponents.Day.Value;
                releaseDate = new DateOnly(year, month, day);
            }

            var viewModel = new GeniusSongPanelViewModel
            {
                Culture = context.Culture,
                ArtistName = mostViewedHit.Result.ArtistNames,
                Title = mostViewedHit.Result.Title,
                ReleaseDate = releaseDate == default
                    ? string.Empty
                    : releaseDate.ToString("d", context.Culture),
                ThumbnailUrl = thumbnailUrl,
                ThumbnailWidth = width,
                ThumbnailHeight = height,
                LyricsUrl = mostViewedHit.Result.Url,
                PageViews = mostViewedHit.Result.Stats.Pageviews ?? 0
            };

            var template = await _templatesManager.GetTemplateAsync("Misc/Genius/GeniusSongPanel", viewModel);
            context.ReplyHtml(template.RemoveNewlines());
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while fetching song");
        }
    }
}