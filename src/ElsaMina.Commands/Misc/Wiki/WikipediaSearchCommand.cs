using System.Web;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.Wiki;

[NamedCommand("wikipedia", "wiki")]
public class WikipediaSearchCommand : Command
{
    private const string WIKIPEDIA_API_URL = "https://{0}.wikipedia.org/w/api.php";

    private readonly IHttpService _httpService;

    public WikipediaSearchCommand(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "wikipedia_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var languageCode = context.Culture.TwoLetterISOLanguageName;
            var page = await SearchPage(context.Target, languageCode, cancellationToken);

            if (page == null)
            {
                context.ReplyRankAwareLocalizedMessage("wiki_page_not_found");
                return;
            }

            var pageData = await FetchPageDetails(page.PageId, languageCode, cancellationToken);
            if (pageData == null)
            {
                context.ReplyRankAwareLocalizedMessage("wiki_page_not_found");
                return;
            }

            var line = pageData.Extract?.Split('\n').FirstOrDefault() ?? string.Empty;
            var imageTag = string.Empty;
            var thumbnail = pageData.Thumbnail;
            if (thumbnail != null)
            {
                var (width, height) =
                    ImageUtils.ResizeWithSameAspectRatio(thumbnail.Width, thumbnail.Height, 150, 120);
                imageTag =
                    $"""<img src="{thumbnail.Source}" width="{width}" height="{height}" style="float:left; margin-right: 8px;"/>""";
            }

            var box =
                $"""{imageTag}{line} <a href="https://{languageCode}.wikipedia.org/wiki/{page.Title}">{page.Title}</a><br>""";
            context.ReplyHtml(box, rankAware: true);
        }
        catch (Exception ex)
        {
            context.ReplyRankAwareLocalizedMessage("wiki_error", ex.Message);
        }
    }

    private async Task<WikiExtractPage> FetchPageDetails(int pageId, string languageCode,
        CancellationToken cancellationToken)
    {
        var queryParameters = new Dictionary<string, string>
        {
            ["action"] = "query",
            ["format"] = "json",
            ["prop"] = "extracts|pageimages",
            ["pageids"] = pageId.ToString(),
            ["exintro"] = "true",
            ["explaintext"] = "true",
            ["piprop"] = "thumbnail",
            ["pithumbsize"] = "200"
        };
        var pageDataResponse = await _httpService.SendAsync<WikipediaExtractResponse>(
            HttpRequest.Get(string.Format(WIKIPEDIA_API_URL, languageCode)).WithQueryParameters(queryParameters),
            cancellationToken);
        var pageData = pageDataResponse.Data?.Query?.Pages?.Values.FirstOrDefault();
        return pageData;
    }

    private async Task<WikiPage> SearchPage(string searchTerms, string languageCode,
        CancellationToken cancellationToken)
    {
        var queryParameters = new Dictionary<string, string>
        {
            ["action"] = "query",
            ["generator"] = "search",
            ["gsrsearch"] = searchTerms,
            ["prop"] = "pageprops",
            ["format"] = "json"
        };

        var listQueryResponse = await _httpService.SendAsync<WikipediaApiSearchResponse>(
            HttpRequest.Get(string.Format(WIKIPEDIA_API_URL, languageCode)).WithQueryParameters(queryParameters),
            cancellationToken);
        var pages = listQueryResponse.Data?.Query?.Pages;
        if (pages == null || pages.Count == 0)
        {
            return null;
        }

        var page = pages
            .Values
            .OrderBy(page => page.Index)
            .FirstOrDefault(page => page.PageProps == null || !page.PageProps.ContainsKey("disambiguation"));
        return page;
    }
}