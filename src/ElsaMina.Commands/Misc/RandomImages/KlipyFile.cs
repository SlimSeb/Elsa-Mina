using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipyFile
{
    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("size")]
    public long Size { get; set; }
}
