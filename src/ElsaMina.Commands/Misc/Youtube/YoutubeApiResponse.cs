using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Youtube;

public class PageInfo
{
    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    [JsonPropertyName("resultsPerPage")]
    public int ResultsPerPage { get; set; }
}

public class VideoId
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("videoId")]
    public string VideoIdValue { get; set; }
}

public class Thumbnail
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public class Thumbnails
{
    [JsonPropertyName("default")]
    public Thumbnail Default { get; set; }

    [JsonPropertyName("medium")]
    public Thumbnail Medium { get; set; }

    [JsonPropertyName("high")]
    public Thumbnail High { get; set; }
}

public class Snippet
{
    [JsonPropertyName("publishedAt")]
    public string PublishedAt { get; set; }

    [JsonPropertyName("channelId")]
    public string ChannelId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("thumbnails")]
    public Thumbnails Thumbnails { get; set; }

    [JsonPropertyName("channelTitle")]
    public string ChannelTitle { get; set; }

    [JsonPropertyName("liveBroadcastContent")]
    public string LiveBroadcastContent { get; set; }

    [JsonPropertyName("publishTime")]
    public string PublishTime { get; set; }
}

public class SearchResultItem
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("etag")]
    public string ETag { get; set; }

    [JsonPropertyName("id")]
    public VideoId Id { get; set; }

    [JsonPropertyName("snippet")]
    public Snippet Snippet { get; set; }
}

public class YouTubeSearchResponse
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("etag")]
    public string ETag { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string NextPageToken { get; set; }

    [JsonPropertyName("regionCode")]
    public string RegionCode { get; set; }

    [JsonPropertyName("pageInfo")]
    public PageInfo PageInfo { get; set; }

    [JsonPropertyName("items")]
    public List<SearchResultItem> Items { get; set; }
}
