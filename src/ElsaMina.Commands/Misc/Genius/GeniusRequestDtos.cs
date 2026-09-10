using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Genius;

public class GeniusSearchResult
{
    [JsonPropertyName("meta")]
    public Meta Meta { get; set; }

    [JsonPropertyName("response")]
    public Response Response { get; set; }
}

public class Meta
{
    [JsonPropertyName("status")]
    public int Status { get; set; }
}

public class Response
{
    [JsonPropertyName("hits")]
    public List<Hit> Hits { get; set; }
}

public class Hit
{
    [JsonPropertyName("highlights")]
    public List<object> Highlights { get; set; }

    [JsonPropertyName("index")]
    public string Index { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("result")]
    public Result Result { get; set; }
}

public class Result
{
    [JsonPropertyName("annotation_count")]
    public int AnnotationCount { get; set; }

    [JsonPropertyName("api_path")]
    public string ApiPath { get; set; }

    [JsonPropertyName("artist_names")]
    public string ArtistNames { get; set; }

    [JsonPropertyName("full_title")]
    public string FullTitle { get; set; }

    [JsonPropertyName("header_image_thumbnail_url")]
    public string HeaderImageThumbnailUrl { get; set; }

    [JsonPropertyName("header_image_url")]
    public string HeaderImageUrl { get; set; }

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("lyrics_owner_id")]
    public long LyricsOwnerId { get; set; }

    [JsonPropertyName("lyrics_state")]
    public string LyricsState { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("primary_artist_names")]
    public string PrimaryArtistNames { get; set; }

    [JsonPropertyName("pyongs_count")]
    public int? PyongsCount { get; set; }

    [JsonPropertyName("relationships_index_url")]
    public string RelationshipsIndexUrl { get; set; }

    [JsonPropertyName("release_date_components")]
    public ReleaseDateComponents ReleaseDateComponents { get; set; }

    [JsonPropertyName("release_date_for_display")]
    public string ReleaseDateForDisplay { get; set; }

    [JsonPropertyName("release_date_with_abbreviated_month_for_display")]
    public string ReleaseDateWithAbbreviatedMonthForDisplay { get; set; }

    [JsonPropertyName("song_art_image_thumbnail_url")]
    public string SongArtImageThumbnailUrl { get; set; }

    [JsonPropertyName("song_art_image_url")]
    public string SongArtImageUrl { get; set; }

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("title_with_featured")]
    public string TitleWithFeatured { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("featured_artists")]
    public List<Artist> FeaturedArtists { get; set; }

    [JsonPropertyName("primary_artist")]
    public Artist PrimaryArtist { get; set; }

    [JsonPropertyName("primary_artists")]
    public List<Artist> PrimaryArtists { get; set; }
}

public class ReleaseDateComponents
{
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("month")]
    public int? Month { get; set; }

    [JsonPropertyName("day")]
    public int? Day { get; set; }
}

public class Stats
{
    [JsonPropertyName("unreviewed_annotations")]
    public int UnreviewedAnnotations { get; set; }

    [JsonPropertyName("hot")]
    public bool Hot { get; set; }

    [JsonPropertyName("pageviews")]
    public int? Pageviews { get; set; }
}

public class Artist
{
    [JsonPropertyName("api_path")]
    public string ApiPath { get; set; }

    [JsonPropertyName("header_image_url")]
    public string HeaderImageUrl { get; set; }

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; }

    [JsonPropertyName("is_meme_verified")]
    public bool IsMemeVerified { get; set; }

    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("iq")]
    public int? Iq { get; set; }
}
