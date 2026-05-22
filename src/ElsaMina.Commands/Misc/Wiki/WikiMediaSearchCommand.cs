using System.Text.RegularExpressions;
using System.Web;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;

namespace ElsaMina.Commands.Misc.Wiki;

public abstract class WikiMediaSearchCommand : Command
{
    protected abstract string ApiUrl { get; }
    protected abstract string GetPageUrl(string title);

    private const int MAX_THUMBNAIL_IMAGE_WIDTH = 150;
    private const int MAX_THUMBNAIL_IMAGE_HEIGHT = 120;

    private readonly IHttpService _httpService;

    protected WikiMediaSearchCommand(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await SearchPage(context.Target, cancellationToken);
            if (page == null)
            {
                context.ReplyRankAwareLocalizedMessage("wiki_page_not_found");
                return;
            }

            var (extract, thumbnail) = await FetchPageDetails(page.PageId, cancellationToken);
            if (string.IsNullOrWhiteSpace(extract))
            {
                context.ReplyRankAwareLocalizedMessage("wiki_page_not_found");
                return;
            }

            var imageTag = string.Empty;
            if (thumbnail != null)
            {
                var (width, height) = ImageUtils.ResizeWithSameAspectRatio(thumbnail.Width, thumbnail.Height,
                    MAX_THUMBNAIL_IMAGE_WIDTH, MAX_THUMBNAIL_IMAGE_HEIGHT);
                imageTag =
                    $"""<img src="{thumbnail.Source}" width="{width}" height="{height}" style="float:left; margin-right: 8px;"/>""";
            }

            var pageUrl = GetPageUrl(page.Title);
            var box = $"""{imageTag}{extract} <a href="{pageUrl}">{page.Title}</a><br>""";
            context.ReplyHtml(box, rankAware: true);
        }
        catch (Exception ex)
        {
            context.ReplyRankAwareLocalizedMessage("wiki_error", ex.Message);
        }
    }

    private async Task<WikiPage> SearchPage(string searchTerms, CancellationToken cancellationToken)
    {
        var queryParameters = new Dictionary<string, string>
        {
            ["action"] = "query",
            ["generator"] = "search",
            ["gsrsearch"] = searchTerms,
            ["prop"] = "pageprops",
            ["format"] = "json"
        };
        var response = await _httpService.GetAsync<WikipediaApiSearchResponse>(
            ApiUrl, queryParams: queryParameters, cancellationToken: cancellationToken);
        var pages = response.Data?.Query?.Pages;
        if (pages == null || pages.Count == 0)
        {
            return null;
        }

        return pages
            .Values
            .OrderBy(page => page.Index)
            .FirstOrDefault(page => page.PageProps == null || !page.PageProps.ContainsKey("disambiguation"));
    }

    private async Task<(string extract, Thumbnail thumbnail)> FetchPageDetails(
        int pageId, CancellationToken cancellationToken)
    {
        var wikitextTask = FetchWikitext(pageId, cancellationToken);
        var thumbnailTask = FetchThumbnail(pageId, cancellationToken);

        await Task.WhenAll(wikitextTask, thumbnailTask);

        var extract = ExtractFirstParagraph(await wikitextTask);
        return (extract, await thumbnailTask);
    }

    private async Task<string> FetchWikitext(int pageId, CancellationToken cancellationToken)
    {
        var queryParameters = new Dictionary<string, string>
        {
            ["action"] = "parse",
            ["format"] = "json",
            ["pageid"] = pageId.ToString(),
            ["prop"] = "wikitext"
        };
        var response = await _httpService.GetAsync<PokepediaParseResponse>(
            ApiUrl, queryParams: queryParameters, cancellationToken: cancellationToken);
        return response.Data?.Parse?.Wikitext?.Content ?? string.Empty;
    }

    private async Task<Thumbnail> FetchThumbnail(int pageId, CancellationToken cancellationToken)
    {
        var queryParameters = new Dictionary<string, string>
        {
            ["action"] = "query",
            ["format"] = "json",
            ["prop"] = "pageimages",
            ["pageids"] = pageId.ToString(),
            ["piprop"] = "thumbnail",
            ["pithumbsize"] = "200"
        };
        var response = await _httpService.GetAsync<WikipediaExtractResponse>(
            ApiUrl, queryParams: queryParameters, cancellationToken: cancellationToken);
        return response.Data?.Query?.Pages?.Values.FirstOrDefault()?.Thumbnail;
    }

    private static string ExtractFirstParagraph(string wikitext)
    {
        var text = RemoveLeadingTemplateBlocks(wikitext);
        return text
                   .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
                   .Select(CleanWikiMarkup)
                   .Select(paragraph => paragraph.Trim())
                   .FirstOrDefault(paragraph => paragraph.Length > 0
                                                && !paragraph.StartsWith("==")
                                                && !paragraph.StartsWith('{'))
               ?? string.Empty;
    }

    private static string RemoveLeadingTemplateBlocks(string wikitext)
    {
        var text = wikitext.TrimStart();
        var changed = true;
        while (changed)
        {
            changed = false;
            if (text.StartsWith("{{"))
            {
                var depth = 0;
                var endIndex = 0;
                for (var i = 0; i < text.Length - 1; i++)
                {
                    if (text[i] == '{' && text[i + 1] == '{')
                    {
                        depth++;
                        i++;
                    }
                    else if (text[i] == '}' && text[i + 1] == '}')
                    {
                        depth--;
                        i++;
                        if (depth == 0)
                        {
                            endIndex = i + 1;
                            break;
                        }
                    }
                }

                if (endIndex == 0)
                {
                    break;
                }

                text = text[(endIndex + 1)..].TrimStart();
                changed = true;
            }
            else if (text.StartsWith("{|"))
            {
                var depth = 0;
                var endIndex = 0;
                for (var i = 0; i < text.Length - 1; i++)
                {
                    if (text[i] == '{' && text[i + 1] == '|')
                    {
                        depth++;
                        i++;
                    }
                    else if (text[i] == '|' && text[i + 1] == '}')
                    {
                        depth--;
                        i++;
                        if (depth == 0)
                        {
                            endIndex = i + 1;
                            break;
                        }
                    }
                }

                if (endIndex == 0)
                {
                    break;
                }

                text = text[(endIndex + 1)..].TrimStart();
                changed = true;
            }
            else if (text.StartsWith("|}"))
            {
                var newline = text.IndexOf('\n');
                text = (newline >= 0 ? text[(newline + 1)..] : string.Empty).TrimStart();
                changed = true;
            }
        }

        return text;
    }

    private static string CleanWikiMarkup(string text)
    {
        // Remove [[File:...]] / [[Image:...]] / [[Fichier:...]]
        text = Regex.Replace(text, @"\[\[(File|Image|Fichier):[^\]]+\]\]", string.Empty, RegexOptions.IgnoreCase,
            Constants.REGEX_MATCH_TIMEOUT);
        // [[link|display text]] → display text
        text = Regex.Replace(text, @"\[\[[^\]]+\|([^\]]+)\]\]", "$1", RegexOptions.Compiled,
            Constants.REGEX_MATCH_TIMEOUT);
        // [[link]] → link
        text = Regex.Replace(text, @"\[\[([^\]]+)\]\]", "$1", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);
        // {{Template|arg}} → last pipe segment (e.g. {{Jeu|DP}} → DP)
        text = Regex.Replace(text, @"\{\{[^{}]+\}\}", match =>
        {
            var parts = match.Value[2..^2].Split('|');
            return parts.Length > 1 ? parts[^1] : string.Empty;
        }, RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);
        // Bold and italic
        text = text.Replace("'''", string.Empty).Replace("''", string.Empty);
        // HTML tags
        text = Regex.Replace(text, @"<[^>]+>", string.Empty, RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);
        // Collapse whitespace
        return Regex.Replace(text, @"\s+", " ", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT).Trim();
    }
}