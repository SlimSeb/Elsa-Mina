using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.RandomImages;

public class UnsplashPhotoDto
{
    [JsonPropertyName("urls")]
    public UnsplashPhotoUrlsDto Urls { get; set; }
}

public class UnsplashPhotoUrlsDto
{
    [JsonPropertyName("regular")]
    public string Regular { get; set; }
}
