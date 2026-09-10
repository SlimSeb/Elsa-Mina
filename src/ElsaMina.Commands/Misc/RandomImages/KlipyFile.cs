using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipyFile
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
