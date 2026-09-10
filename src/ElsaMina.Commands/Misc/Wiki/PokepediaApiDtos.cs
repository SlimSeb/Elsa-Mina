using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Wiki;

public class PokepediaParseResponse
{
    [JsonPropertyName("parse")]
    public PokepediaParseResult Parse { get; set; }
}

public class PokepediaParseResult
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("pageid")]
    public int PageId { get; set; }

    [JsonPropertyName("wikitext")]
    public PokepediaWikitext Wikitext { get; set; }
}

public class PokepediaWikitext
{
    [JsonPropertyName("*")]
    public string Content { get; set; }
}
