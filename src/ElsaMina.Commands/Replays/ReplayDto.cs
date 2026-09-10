using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Replays;

public class ReplayDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("format")]
    public string Format { get; set; }
    [JsonPropertyName("players")]
    public List<string> Players { get; set; } = [];
    [JsonPropertyName("log")]
    public string Log { get; set; }
    [JsonPropertyName("uploadtime")]
    public long UploadTime { get; set; }
    [JsonPropertyName("views")]
    public int Views { get; set; }
    [JsonPropertyName("rating")]
    public int? Rating { get; set; }
    [JsonPropertyName("formatid")]
    public string FormatId { get; set; }
    [JsonPropertyName("private")]
    public int Private { get; set; }
    [JsonPropertyName("password")]
    public string Password { get; set; }
}
