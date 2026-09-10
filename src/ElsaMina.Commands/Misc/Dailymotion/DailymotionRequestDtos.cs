using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ElsaMina.Commands.Misc.Dailymotion;

public class VideoListResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("explicit")]
    public bool Explicit { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    [JsonPropertyName("list")]
    public List<VideoItem> List { get; set; } = [];
}

public class VideoItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("thumbnail_url")]
    public string ThumbnailUrl { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("views_total")]
    public int ViewsTotal { get; set; }

    [JsonPropertyName("likes_total")]
    public int LikesTotal { get; set; }

    [JsonPropertyName("explicit")]
    public bool Explicit { get; set; }
}