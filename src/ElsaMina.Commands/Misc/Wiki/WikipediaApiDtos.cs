using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Wiki;

public class WikipediaApiSearchResponse
{
    [JsonPropertyName("batchcomplete")]
    public string BatchComplete { get; set; }

    [JsonPropertyName("continue")]
    public Continue Continue { get; set; }

    [JsonPropertyName("query")]
    public QueryPages Query { get; set; }
}

public class Continue
{
    [JsonPropertyName("gsroffset")]
    public int GsrOffset { get; set; }

    [JsonPropertyName("continue")]
    public string ContinueToken { get; set; }
}

public class QueryPages
{
    [JsonPropertyName("pages")]
    public Dictionary<string, WikiPage> Pages { get; set; }
}

public class WikiPage
{
    [JsonPropertyName("pageid")]
    public int PageId { get; set; }

    [JsonPropertyName("ns")]
    public int Namespace { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("pageprops")]
    public IDictionary<string, string> PageProps { get; set; }
}

public class WikipediaExtractResponse
{
    [JsonPropertyName("batchcomplete")]
    public string BatchComplete { get; set; }

    [JsonPropertyName("query")]
    public QueryWithExtract Query { get; set; }
}

public class QueryWithExtract
{
    [JsonPropertyName("normalized")]
    public List<NormalizedTitle> Normalized { get; set; }

    [JsonPropertyName("pages")]
    public Dictionary<string, WikiExtractPage> Pages { get; set; }
}

public class NormalizedTitle
{
    [JsonPropertyName("from")]
    public string From { get; set; }

    [JsonPropertyName("to")]
    public string To { get; set; }
}

public class WikiExtractPage
{
    [JsonPropertyName("pageid")]
    public int PageId { get; set; }

    [JsonPropertyName("ns")]
    public int Namespace { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("extract")]
    public string Extract { get; set; }

    [JsonPropertyName("thumbnail")]
    public Thumbnail Thumbnail { get; set; }
}

public class Thumbnail
{
    [JsonPropertyName("source")]
    public string Source { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
